using System.Configuration;
using Microsoft.Extensions.Configuration;
using WalleeCharging.Price;
using WalleeCharging.ManualTests;
using System.Text;

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
    Console.WriteLine("Enter year and month to fetch prices for.");
    Console.WriteLine("Year (4 digits):");
    string? yearText = Console.ReadLine();
    Console.WriteLine("Month (1 or 2 digits):");
    string? monthText = Console.ReadLine();
    if ((string.IsNullOrEmpty(yearText)) || string.IsNullOrEmpty(monthText))
    {
        Console.WriteLine("ERROR: Unable to parse!");
        return;
    }
    int year = Int32.Parse(yearText);
    int month = Int32.Parse(monthText);

    // Where to save


    // Get the API token via the dotnet user secrets.
    var config = new ConfigurationBuilder()
        .AddUserSecrets<Program>()
        .Build();
    string entsoeApiToken = config["EntsoeApiKey"] ?? throw new ConfigurationErrorsException("missing EntsoeApiKey");

    var priceFetcher = new EntsoePriceFetcher(entsoeApiToken);

    // Get prices
    string targetFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        $"{year:d4}{month:d2}.csv");
    DateTime firstDay = new DateTime(year, month, 1, 0, 0, 0, 0, DateTimeKind.Local);
    DateTime day = firstDay;
    using (var fileWriter = new StreamWriter(targetFilePath))
    {
        fileWriter.WriteLine($"Time,Price");
        while (day.Month == firstDay.Month)
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
    Console.WriteLine($"Done! Prices written to '{targetFilePath}'");
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