
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Utility;

namespace WalleeCharging.ChargingStation;

/// <summary>
/// Implementation of a modbus master for a Alfen Eve charging station.
/// </summary>
/// <remarks>
/// <para>
/// This implementation only controls socket 1, because it was only tested against
/// a single socket charging station.
/// </para>
/// <para>
/// The Alfen Eve charging station needs to be configured for active load balancing,
/// with the "data source" set to "Energy Management System". Otherwise it will
/// not act as a modbus slave, and will not accept connections.
/// See Alfen documentation at https://alfen.com/file-download/download/public/1610.
/// </para>
/// </remarks>
public class AlfenEveModbusChargingStation : IChargingStation, IDisposable
{
    private readonly int PORT = 502;
    private readonly byte CHARGING_SOCKET_ID = 1;
    private readonly ushort REGISTER_CURRENT_PHASE_1 = 320;
    private readonly ushort REGISTER_REAL_POWER_SUM = 344;
    private readonly ushort REGISTER_MAX_CURRENT_SETPOINT = 1210;
    private readonly int WARN_TRESHOLD_MILLISECONDS = 500;

    private readonly string _hostname;
    private readonly ILogger<AlfenEveModbusChargingStation> _logger;
    private readonly Stopwatch _stopWatch;
    private readonly ModbusFactory _modbusFactory;
    private TcpClient _tcpClient;
    private IModbusMaster _modbusMaster;

    public AlfenEveModbusChargingStation(string hostname, ILogger<AlfenEveModbusChargingStation> logger)
    {
        _hostname = hostname;
        
        _logger = logger;
        _stopWatch = new Stopwatch();

        _modbusFactory = new ModbusFactory();
        _tcpClient = new TcpClient(_hostname, PORT);
        _modbusMaster = _modbusFactory.CreateMaster(_tcpClient);
    }

    public void Dispose()
    {
        _modbusMaster.Dispose();
        _tcpClient.Dispose();
    }

    public async Task<ChargingStationData> GetChargingStationDataAsync()
    {
        try
        {
            ConnectIfNeeded();


            var data = new ChargingStationData();

            _stopWatch.Reset();
            _stopWatch.Start();
            ushort count = (ushort)(REGISTER_REAL_POWER_SUM - REGISTER_CURRENT_PHASE_1 + 2);
            ushort[] registerValues = await _modbusMaster.ReadHoldingRegistersAsync(CHARGING_SOCKET_ID, REGISTER_CURRENT_PHASE_1, count);
            _stopWatch.Stop();

            var logLevel = _stopWatch.ElapsedMilliseconds >= WARN_TRESHOLD_MILLISECONDS ? LogLevel.Warning : LogLevel.Debug;
            _logger.Log(logLevel, "Getting currents and RealPowerSum over modbus took {millis} milliseconds", _stopWatch.ElapsedMilliseconds);

            data.Current1 = ModbusUtility.GetSingle(registerValues[0], registerValues[1]);
            data.Current2 = ModbusUtility.GetSingle(registerValues[2], registerValues[3]);
            data.Current3 = ModbusUtility.GetSingle(registerValues[4], registerValues[5]);

            int offset = REGISTER_REAL_POWER_SUM - REGISTER_CURRENT_PHASE_1;
            data.RealPowerSum = ModbusUtility.GetSingle(registerValues[offset], registerValues[offset+1]);
            
            return data;
        }
        catch (Exception e) when (e is IOException || e is SocketException || e is SlaveException)
        {
            throw new ChargingStationException($"Failed to fetch data from charging station at {_hostname}", e);
        }
    }

    public async Task SetCurrentLimitAsync(float currentLimitAmpere)
    {
        try
        {
            ConnectIfNeeded();

            ushort[] newRegisterValues = FloatToRegisterValues(currentLimitAmpere);

            _stopWatch.Reset();
            _stopWatch.Start();
            await _modbusMaster.WriteMultipleRegistersAsync(CHARGING_SOCKET_ID, REGISTER_MAX_CURRENT_SETPOINT, newRegisterValues);
            _stopWatch.Stop();

            var logLevel = _stopWatch.ElapsedMilliseconds >= WARN_TRESHOLD_MILLISECONDS ? LogLevel.Warning : LogLevel.Debug;
            _logger.Log(logLevel, "Setting current limit over modbus took {millis} milliseconds", _stopWatch.ElapsedMilliseconds);
        }
        catch (Exception e) when (e is IOException || e is SocketException || e is SlaveException)
        {
            throw new ChargingStationException($"Failed to send current limit to charging station at {_hostname}", e);
        }
    }

    private void ConnectIfNeeded()
    {
        if (!_tcpClient.Connected)
        {
            _logger.LogWarning("Reconnecting to charging station.");

            _modbusMaster.Dispose();
            _tcpClient.Dispose();
            
            _tcpClient = new TcpClient(_hostname, PORT);
            _modbusMaster = _modbusFactory.CreateMaster(_tcpClient);
        }
    }

    /// <summary>
    /// Convert a float into two 16-bit modbus register values.
    /// </summary>
    /// <param name="floatValue">The float to convert.</param>
    /// <returns>An array with two modbus register values.</returns>
    private static ushort[] FloatToRegisterValues(float floatValue)
    {
        // Modbus only supports 16-bit values natively.
        // So we are transporting 32-bit floats as two 16-bit values.
        // This means we need to be aware of the byte order used by the modbus slave.

        // The byte order of the 32-bit values in Alfen Eve modbus slave implementation is
        // "Network order" aka "Big Endian". This is reversed from the typical Little Endian
        // used by most CPUs that .NET would run on.

        // Note that we do not reverse the byte order within the 16-bit values though.
        // The NModbus implementation already takes care of that in RegisterCollection.NetworkBytes.
        // Yes, it's confusing.

        // To summarize how the bytes of the float value are reversed:
        //
        // 1. We split the float into 4 bytes                                   b1, b2, b3, b4
        // 2. We use to 4 bytes to build two 16-bit values                      (b3 b4), (b1 b2)
        // 3. NModbus sends the bytes of the 16-bit values in network order     (b4 b3), (b2 b1)
        
        byte[] bytes = BitConverter.GetBytes(floatValue);
        ushort[] registerValues = new ushort[2];
        if (BitConverter.IsLittleEndian)
        {
            registerValues[0] = BitConverter.ToUInt16(bytes, 2);
            registerValues[1] = BitConverter.ToUInt16(bytes, 0);
        }
        else
        {
            // unlikely, but maybe some embedded systems are big endian
            registerValues[0] = BitConverter.ToUInt16(bytes, 0);
            registerValues[1] = BitConverter.ToUInt16(bytes, 2);
        }
        return registerValues;
    }
}
