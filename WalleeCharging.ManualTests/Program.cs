using System.Configuration;
using Microsoft.Extensions.Configuration;
using WalleeCharging.Price;

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