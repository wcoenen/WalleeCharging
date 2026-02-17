using Microsoft.Extensions.Logging;
using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;

namespace WalleeCharging.Control;

public class PricePolicy : IChargingPolicy
{
    private readonly IDatabase _database;
    private readonly ILogger<PricePolicy> _logger;

    public PricePolicy(IDatabase database, ILogger<PricePolicy> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<ChargingPolicyResult> EvaluateAsync(ChargingStationData? chargingStationData, MeterData? meterData)
    {
        var maxPriceEurocentPerMWh = await _database.GetParameterAsync("MaxPriceEurocentPerMWh");
        var currentPrice = await _database.GetPriceAsync(DateTime.UtcNow);

        if (currentPrice == null)
        {
            _logger.LogDebug("Price is unknown");
            return new ChargingPolicyResult
            {
                CurrentLimitAmpere = 0,
                Message = "Price is unknown."
            };
        }

        if (currentPrice.PriceEurocentPerMWh > maxPriceEurocentPerMWh)
        {
            _logger.LogDebug("Price is too high: {currentPrice} > {maxPrice}",
                currentPrice.PriceEurocentPerMWh, maxPriceEurocentPerMWh);
            return new ChargingPolicyResult
            {
                CurrentLimitAmpere = 0,
                Message = $"Price is too high: {currentPrice.PriceEurocentPerMWh} > {maxPriceEurocentPerMWh}"
            };
        }

        _logger.LogDebug("Price is acceptable: {currentPrice} <= {maxPrice}",
            currentPrice.PriceEurocentPerMWh, maxPriceEurocentPerMWh);
        return new ChargingPolicyResult
        {
            CurrentLimitAmpere = float.MaxValue,
            Message = "Price is acceptable."
        };
    }
}
