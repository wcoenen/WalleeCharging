using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using WalleeCharging.Meter;
using WalleeCharging.Price;

Console.WriteLine("[1] EntsoePriceFetcher");
Console.WriteLine("[2] P1PortReader");
Console.WriteLine("Test choice: ");
string? choice = Console.ReadLine();
switch (choice)
{
    case "1": 
        await TestEntsoePriceFetcherAsync();
        break;
    case "2":
        await TestP1PortReaderAsync();
        break;
    default: 
        Environment.Exit(0);
        break;
}

async Task TestEntsoePriceFetcherAsync()
{
    // Get the API token via the dotnet user secrets.
    var config = new ConfigurationBuilder()
        .AddUserSecrets<Program>()
        .Build();
    string entsoeApiToken = config["EntsoeApiKey"] ?? throw new ConfigurationErrorsException("missing EntsoeApiKey");

    var priceFetcher = new EntsoePriceFetcher(entsoeApiToken);

    // Get November prices
    DateTime day = new DateTime(2023, 11, 1, 0, 0, 0, 0, DateTimeKind.Local);

    using (var fileWriter = new StreamWriter(@"c:\users\wim\november.csv"))
    {
        fileWriter.WriteLine($"Time,Price");
        while (day.Month == 11)
        {
            Console.WriteLine($"Fetching prices for {day}");
            var prices = await priceFetcher.GetPricesAsync(day.ToUniversalTime(), CancellationToken.None);
            foreach (ElectricityPrice price in prices)
            {
                string line = $"{price.Time:o},{price.PriceEurocentPerMWh}";
                Console.WriteLine(line);
                fileWriter.WriteLine(line);
            }
            day = day.AddDays(1);
            await Task.Delay(1000);
        }
    }
}

async Task TestP1PortReaderAsync()
{
    await Task.Delay(100);

    using (var serialPort = new SerialPort())
    {
        serialPort.PortName = "/dev/ttyUSB0";
        serialPort.BaudRate = 115200;
        serialPort.Handshake = Handshake.XOnXOff;
        serialPort.ReadTimeout = 2000;
        serialPort.Open();

        var buffer = new byte[2048];
        while (true)
        {
            int size = ReadP1Telegram(serialPort.BaseStream, buffer);
            var meterData = ParseP1Telegram(buffer, size);
            Console.WriteLine(meterData);
        }

    }
}

int HexByteToInt(int hexByte)
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

int ReadP1Telegram(Stream stream, byte[] buffer)
{
    int offset = 0;
    buffer[0] = 0x00;

    // Find start '/'
    while (buffer[0] != (byte)'/')
    {
        stream.ReadExactly(buffer, 0, 1);
    }

    // Read until end '!'
    while (buffer[offset] != (byte)'!')
    {
        offset += 1;
        if (offset >= buffer.Length)
        {
            throw new InvalidDataException("Encountered telegram larger than buffer");
        }
        stream.ReadExactly(buffer, offset, 1);
    }

    int sizeWithoutChecksum = offset+1;

    // Read 4 additional checksum hex characters and verify
    if (offset+4 >= buffer.Length)
    {
        throw new InvalidDataException("Encountered telegram+checksum larger than buffer");
    }
    stream.ReadExactly(buffer, offset+1, 4);
    int a = HexByteToInt(buffer[offset+1]);
    int b = HexByteToInt(buffer[offset+2]);
    int c = HexByteToInt(buffer[offset+3]);
    int d = HexByteToInt(buffer[offset+4]);
    ushort checksum = (ushort)((a<<12)|(b<<8)|(c<<4)|d);
    ushort calculatedChecksum = CRC16(buffer, sizeWithoutChecksum);
    if (checksum != calculatedChecksum)
    {
        throw new InvalidDataException("Received P1 port telegram with invalid checksum!");
    }
    
    return sizeWithoutChecksum;
}

void CheckUnit(string line, string actualUnit, string expectedUnit)
{
    if (actualUnit != expectedUnit) 
    {
        throw new InvalidDataException("Expected unit '{expectedUnit}' but got '{actualUnit}' in '{line}'");
    }
}

MeterData ParseP1Telegram(byte[] buffer, int size)
{

    // example line:
    // 1-0:31.7.0(002.52*A)
    //
    // which parsers to:
    //      obisCode = "1-0:31.7.0"
    //      measurementValue = "002.52"
    //      measurementUnit = "A"
    var regex = new Regex(@"(?<obisCode>[\d\.:\-]+)\((?<measurementValue>[^*\)]+)\*(?<measurementUnit>[^\)]+)\)");

    var meterData = new MeterData();

    using (var stream = new MemoryStream(buffer, 0, size))
    using (var textReader = new StreamReader(stream, Encoding.ASCII))
    {
        string? line;
        while ((line = textReader.ReadLine()) != null)
        {
            var match = regex.Match(line);
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

// Ported to C# from C code at https://github.com/jantenhove/P1-Meter-ESP8266/blob/master/CRC16.h
// GPLv3 license
ushort CRC16(byte[] buffer, int len)
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