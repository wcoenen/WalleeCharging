using System.Configuration;
using Microsoft.Extensions.Configuration;
using WalleeCharging.Price;
using WalleeCharging.ManualTests;

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
    var logger = new ConsoleLogger<P1MeterDataProvider>();
    await using (var meterDataProvider = new P1MeterDataProvider(logger))
    {
        while (true)
        {
            Console.WriteLine(await meterDataProvider.GetMeterDataAsync());
        }
    }

    /*
    using (var serialPort = new SerialPort())
    {
        serialPort.PortName = "/dev/ttyUSB0";
        serialPort.BaudRate = 115200;
        serialPort.Handshake = Handshake.XOnXOff;
        serialPort.ReadTimeout = 2000;
        serialPort.Open();

        while (true)
        {
            Console.Write(serialPort.ReadTo("!"));
            Console.WriteLine(serialPort.ReadLine());
        }
    }
    */
}