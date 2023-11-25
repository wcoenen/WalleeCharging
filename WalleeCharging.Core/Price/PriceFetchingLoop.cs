using WalleeCharging.Database;
using WalleeCharging.Price;

namespace WalleeCharging.Price;

public class PriceFetchingLoop
{
    private readonly IPriceFetcher _priceFetcher;
    private readonly IDatabase _database;
    private readonly string CET_TIMEZONE = "Central European Standard Time";

    public PriceFetchingLoop(IPriceFetcher priceFetcher, IDatabase database)
    {
        _priceFetcher = priceFetcher;
        _database = database;
    }

    private async Task FetchPricesIfMissingAsync(DateTime day, CancellationToken cancellationToken)
    {
        if (await _database.GetPriceAsync(day) == null)
        {
            await Task.Delay(1000, cancellationToken); // rate limit
            var prices = await _priceFetcher.GetPricesAsync(day, cancellationToken);
            if (prices != null)
            {
                await _database.SavePricesAsync(prices);
            }
        }
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) 
        {
            var now = DateTime.Now;
            var today = now.Date;

            // fetch today's prices (if we don't have them yet)
            await FetchPricesIfMissingAsync(today.ToUniversalTime(), stoppingToken);
           
            // fetch tomorrow's prices if they should be available by now (and we don't have them yet)
            var currentTimeCET = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, CET_TIMEZONE);
            if (currentTimeCET.Hour >= 14)
            {
                var tomorrow = today.AddDays(1);
                await FetchPricesIfMissingAsync(tomorrow.ToUniversalTime(), stoppingToken);
            }

            // repeat every hour
            await Task.Delay(1000*60*60, stoppingToken);
        }
    }

}