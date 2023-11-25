using EasyModbus;

Console.WriteLine("Hello, World!");

var modbusClient = new ModbusClient("192.168.1.28", 502);
modbusClient.UnitIdentifier = 1;
modbusClient.ConnectedChanged  += (sender) =>
{
    if (modbusClient == null)
        throw new InvalidOperationException();

    if (modbusClient.Connected)
    {
        Console.WriteLine("Connected!");
    }
    else
    {
        Console.WriteLine("Disconnected!");
    }
};

modbusClient.Connect();

// The Alfen Eve registers are typically 32-bit floats, so the value is spread over two 16-bit modbus registers.
// The max current value is stored in 1210 and 1211.
int REGISTER_MAX_CURRENT_SETPOINT = 1210;

// The max current can be restricted by settings other than the above setpoint, e.g. the configured limit.
// The actually applied limit can be read from registers 1100 and 1101
int REGISTER_MAX_CURRENT_ACTUAL = 1100;

// 1. read max current
int[] registerValues1 = modbusClient.ReadHoldingRegisters(REGISTER_MAX_CURRENT_SETPOINT, 2);
float value1 = ModbusClient.ConvertRegistersToFloat(registerValues1, ModbusClient.RegisterOrder.HighLow);
Console.WriteLine($"read max current, before: {value1}");


while (true)
{
    // 2. write max current setpoing
    Console.WriteLine("Enter max current setpoint:");
    string? input = Console.ReadLine();
    if (input != null)
    {
        float value2 = float.Parse(input);
        int[] registerValues2 = ModbusClient.ConvertFloatToRegisters(value2, ModbusClient.RegisterOrder.HighLow);
        modbusClient.WriteMultipleRegisters(REGISTER_MAX_CURRENT_SETPOINT, registerValues2);
    }

    // 3. read max current setpoint
    int[] registerValues3 = modbusClient.ReadHoldingRegisters(REGISTER_MAX_CURRENT_SETPOINT, 2);
    float value3 = ModbusClient.ConvertRegistersToFloat(registerValues3, ModbusClient.RegisterOrder.HighLow);
    Console.WriteLine($"read max current setpoint: {value3}");

    // 4. read max current, actual
    //int[] registerValues4 = modbusClient.ReadHoldingRegisters(REGISTER_MAX_CURRENT_ACTUAL, 2);
    //float value4 = ModbusClient.ConvertRegistersToFloat(registerValues3, ModbusClient.RegisterOrder.HighLow);
    //Console.WriteLine($"read max current setpoint: {value4}");
}

/*
// 4. read "1214"
int value4 = modbusClient.ReadHoldingRegisters(1214, 1)[0];
Console.WriteLine($"setpoint accounted for: {value4}");

// 5. read "1215"
int value5 = modbusClient.ReadHoldingRegisters(1215, 1)[0];
Console.WriteLine($"phases: {value5}");

// 6. read "1431"
//int value6 = modbusClient.ReadHoldingRegisters(1431, 1)[0];
//Console.WriteLine($"max current enabled: {value6}");

Console.WriteLine("The end");

*/