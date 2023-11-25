using System.Configuration;
using Microsoft.Extensions.Configuration;
using WalleeCharging.Price;

// Get the API token via the dotnet user secrets.
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
string entsoeApiToken = config["EntsoeApiKey"] ?? throw new ConfigurationErrorsException("missing EntsoeApiKey");

var priceFetcher = new EntsoePriceFetcher(entsoeApiToken);

var pricesYesterday = await priceFetcher.GetPricesAsync(DateTime.UtcNow.Date.AddDays(-1), CancellationToken.None);
foreach (ElectricityPrice price in pricesYesterday)
{
    Console.WriteLine(price);
}

await Task.Delay(1000);
var pricesToday = await priceFetcher.GetPricesAsync(DateTime.UtcNow.Date, CancellationToken.None);
foreach (ElectricityPrice price in pricesToday)
{
    Console.WriteLine(price);
}

await Task.Delay(1000);
var pricesTomorrow = await priceFetcher.GetPricesAsync(DateTime.UtcNow.Date.AddDays(1), CancellationToken.None);
foreach (ElectricityPrice price in pricesTomorrow)
{
    Console.WriteLine(price);
}