﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WalleeCharging.Database;

namespace WalleeCharging.Price;

public class PriceFetchingLoop : BackgroundService
{
    private readonly IPriceFetcher _priceFetcher;
    private readonly IDatabase _database;
    private readonly ILogger<PriceFetchingLoop> _logger;
    private readonly string CET_TIMEZONE = "Central European Standard Time";

    public PriceFetchingLoop(IPriceFetcher priceFetcher, IDatabase database, ILogger<PriceFetchingLoop> logger)
    {
        _priceFetcher = priceFetcher;
        _database = database;
        _logger = logger;
    }

    private async Task FetchPricesIfMissingAsync(DateTime day, CancellationToken cancellationToken)
    {
        if (await _database.GetPriceAsync(day) == null)
        {
            try
            {
                var prices = await _priceFetcher.GetPricesAsync(day, cancellationToken);
                if ((prices == null) || prices.Length == 0)
                {
                    _logger.LogWarning("Prices for {day:o} were not available.", day);
                }
                else
                {
                    _logger.LogInformation("Saving price {nbPricePoints} price points for {day:o}.", prices.Length, day);
                    await _database.SavePricesAsync(prices);
                }
            }
            catch (PriceFetcherException e)
            {
                _logger.LogError(e, "Error occured when fetching prices for {day:o}", day);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting price fetching loop.");
        try
        {
            while (!stoppingToken.IsCancellationRequested) 
            {
                var now = DateTime.Now;
                var today = now.Date;

                // fetch today's prices (if we don't have them yet)
                await FetchPricesIfMissingAsync(today.ToUniversalTime(), stoppingToken);
            
                // fetch tomorrow's prices if they may be available by now (and we don't have them yet)
                var currentTimeCET = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, CET_TIMEZONE);
                if (currentTimeCET.Hour >= 13 && currentTimeCET.Minute >= 10)
                {
                    var tomorrow = today.AddDays(1);
                    await FetchPricesIfMissingAsync(tomorrow.ToUniversalTime(), stoppingToken);
                }

                // Check every 5 minutes whether there is something to do
                await Task.Delay(5*60*1000, stoppingToken);
            }
        }
        catch (TaskCanceledException e)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                // normal exit during Task.Delay, nothing to do
            }
            else
            {
                _logger.LogCritical(e, "Exiting price fetching loop because of unexpected TaskCanceledException.");
                throw;
            }

        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Exiting price fetching loop because of unexpected exception.");
            throw;
        }
        _logger.LogInformation("Exiting price fetching loop.");
    }

}