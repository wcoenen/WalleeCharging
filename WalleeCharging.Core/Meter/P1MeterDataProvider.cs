using System;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WalleeCharging.Meter;

public class P1MeterDataProvider : IMeterDataProvider, IAsyncDisposable
{
    // example line:
    // 1-0:31.7.0(002.52*A)
    //
    // which parsers to:
    //      obisCode = "1-0:31.7.0"
    //      measurementValue = "002.52"
    //      measurementUnit = "A"
    private static readonly string _regex = @"(?<obisCode>[\d\.:\-]+)\((?<measurementValue>[^*\)]+)\*(?<measurementUnit>[^\)]+)\)";
    private static readonly int SLEEP_TIME_MS = 200;
    private readonly ILogger<P1MeterDataProvider> _logger;
    private readonly object _lock = new object();
    private readonly CancellationTokenSource _stoppingTokenSource = new CancellationTokenSource();
    private readonly Task _serialPortReader;
    private TaskCompletionSource<MeterData>? _taskCompletionSource;

    public P1MeterDataProvider(ILogger<P1MeterDataProvider> logger)
    {
        _logger = logger;
        _serialPortReader = StartSerialPortReader();
    }

    private async Task StartSerialPortReader()
    {
        using (var serialPort = new SerialPort())
        {
            serialPort.PortName = "/dev/ttyUSB0";
            serialPort.BaudRate = 115200;
            serialPort.Handshake = Handshake.XOnXOff;
            serialPort.ReadTimeout = 1; // set to a very low value so we know when the buffer is empty
            serialPort.Open();

            var buffer = new byte[2048];
            while (!_stoppingTokenSource.IsCancellationRequested)
            {
                try
                {
                    int size = await ReadP1Telegram(serialPort, buffer);
                    MeterData meterData = ParseP1Telegram(buffer, size);
                    _logger.LogTrace($"P1 telegram: {meterData}");
                    ProvideMeterData(meterData);
                }
                catch (InvalidDataException e)
                {
                    _logger.LogError(e, "Got invalid meter data from P1 port!");
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Unexpected exception during P1 port reading!");
                    throw;
                }
            }
        }
    }

    public Task<MeterData> GetMeterDataAsync()
    {
        lock (_lock)
        {
            if (_taskCompletionSource == null)
            {
                _taskCompletionSource = new TaskCompletionSource<MeterData>();
            }
            return _taskCompletionSource.Task;
        }
    }

    private void ProvideMeterData(MeterData meterData)
    {
        // Retrieve the value of _taskCompletionSource, and reset to null.
        // This has to be done within a lock to prevent _taskCompletionSource from being set between the two statements,
        // which would then result in the task never completing.
        TaskCompletionSource<MeterData>? taskCompletionSource;
        lock (_lock)
        {
            taskCompletionSource = _taskCompletionSource;
            _taskCompletionSource = null;
        }

        // Provide a result for the GetMeterDataAsync callers that are currently waiting for fresh meter data.
        // This has to be done outside of the lock statement to prevent a deadlock. Why? Because continuations
        // of_taskCompletionSource.Task may run synchronously and call GetMeterDataAsync again, which attempts
        // to take the same lock.
        if (taskCompletionSource != null)
        {
            taskCompletionSource.SetResult(meterData);
        }
    }

    private static int HexByteToInt(int hexByte)
    {
        char hexChar = char.ToUpper((char)hexByte);
        if (hexChar < 'A')
        {
            return hexChar - '0';
        }
        else
        {
            return 10 + (hexChar - 'A');
        }
    }

    private async Task<int> ReadP1Telegram(SerialPort serialPort, byte[] buffer)
    {
        var stream = serialPort.BaseStream;

        // zero out buffer
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = 0x00;
        }

        // wait for bytes to arive
        while (serialPort.BytesToRead == 0)
        {
            await Task.Delay(SLEEP_TIME_MS);
        }

        // Read byte by byte until we find the start marker '/'
        while (buffer[0] != (byte)'/')
        {
            int bytesRead;
            try
            {
                bytesRead = stream.Read(buffer, 0, 1);
            }
            catch (TimeoutException)
            {
                bytesRead = 0;
            }
            if (bytesRead == 0)
            {
                // wait for bytes to arive
                while (serialPort.BytesToRead == 0)
                {
                    await Task.Delay(SLEEP_TIME_MS);
                }
            }
        }
        int offset = 1;

        // Read byte by byte until we find the end marker '!', then read 4 more bytes for the checksum
        int endMarkerOffset = -1;
        while (endMarkerOffset == -1 || offset <= endMarkerOffset+4)
        {
            if (offset >= buffer.Length)
            {
                throw new InvalidDataException("Encountered telegram larger than buffer");
            }
            int bytesRead;
            try
            {
                bytesRead = stream.Read(buffer, offset, 1);
            }
            catch (TimeoutException) 
            {
                bytesRead = 0;
            }
            if (bytesRead == 0)
            {
                // wait for bytes to arive
                while (serialPort.BytesToRead == 0)
                {
                    await Task.Delay(SLEEP_TIME_MS);
                }
            }
            else
            {
                if (buffer[offset] == (byte)'!')
                {
                    endMarkerOffset = offset;
                }
                offset += bytesRead;
            }
        }

        // Verify checksum
        int a = HexByteToInt(buffer[endMarkerOffset+1]);
        int b = HexByteToInt(buffer[endMarkerOffset+2]);
        int c = HexByteToInt(buffer[endMarkerOffset+3]);
        int d = HexByteToInt(buffer[endMarkerOffset+4]);
        ushort checksum = (ushort)((a<<12)|(b<<8)|(c<<4)|d);
        ushort calculatedChecksum = CRC16(buffer, endMarkerOffset+1);
        if (checksum != calculatedChecksum)
        {
            throw new InvalidDataException("Received P1 port telegram with invalid checksum!");
        }
        
        return endMarkerOffset;
    }

    static void CheckUnit(string line, string actualUnit, string expectedUnit)
    {
        if (actualUnit != expectedUnit) 
        {
            throw new InvalidDataException("Expected unit '{expectedUnit}' but got '{actualUnit}' in '{line}'");
        }
    }

    // Ported to C# from C code at https://github.com/jantenhove/P1-Meter-ESP8266/blob/master/CRC16.h
    // GPLv3 license
    private static ushort CRC16(byte[] buffer, int len)
    {
        uint crc = 0;
        for (int pos = 0; pos < len; pos++)
        {
            crc ^= buffer[pos];             // XOR byte into least sig. byte of crc

            for (int i = 8; i != 0; i--)    // Loop over each bit
            {    
                if ((crc & 0x0001) != 0)
                {                           // If the LSB is set
                    crc >>= 1;              // Shift right and XOR 0xA001
                    crc ^= 0xA001;
                }
                else
                {                           // Else LSB is not set
                    crc >>= 1;              // Just shift right
                }
            }
        }

        return (ushort)crc;
    }

    private static MeterData ParseP1Telegram(byte[] buffer, int size)
    {
        var meterData = new MeterData();

        using (var stream = new MemoryStream(buffer, 0, size))
        using (var textReader = new StreamReader(stream, Encoding.ASCII))
        {
            string? line;
            while ((line = textReader.ReadLine()) != null)
            {
                var match = Regex.Match(line, _regex);
                if (match.Success)
                {
                    string obisCode = match.Groups["obisCode"].Value;
                    string measurementValue = match.Groups["measurementValue"].Value;
                    string measurementUnit = match.Groups["measurementUnit"].Value;
                    
                    switch (obisCode)
                    {
                        case "1-0:31.7.0":
                            CheckUnit(line, measurementUnit, "A");
                            meterData.Current1 = float.Parse(measurementValue);
                            break;
                        case "1-0:51.7.0":
                            CheckUnit(line, measurementUnit, "A");
                            meterData.Current2 = float.Parse(measurementValue);
                            break;
                        case "1-0:71.7.0":
                            CheckUnit(line, measurementUnit, "A");
                            meterData.Current3 = float.Parse(measurementValue);
                            break;
                        case "1-0:32.7.0":
                            CheckUnit(line, measurementUnit, "V");
                            meterData.Voltage1 = float.Parse(measurementValue);
                            break;
                        case "1-0:52.7.0":
                            CheckUnit(line, measurementUnit, "V");
                            meterData.Voltage2 = float.Parse(measurementValue);
                            break;
                        case "1-0:72.7.0":
                            CheckUnit(line, measurementUnit, "V");
                            meterData.Voltage3 = float.Parse(measurementValue);
                            break;
                        case "1-0:1.7.0":
                            CheckUnit(line, measurementUnit, "kW");
                            meterData.TotalActivePower = 1000 * float.Parse(measurementValue);
                            break;
                    }
                }
            }
        }

        return meterData;
    }

    public async ValueTask DisposeAsync()
    {
        await _stoppingTokenSource.CancelAsync();
        await _serialPortReader;
        _stoppingTokenSource.Dispose();
    }
}