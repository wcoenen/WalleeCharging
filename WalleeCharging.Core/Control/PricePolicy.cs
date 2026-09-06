using Microsoft.Extensions.Logging;
using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;

namespace WalleeCharging.Control;

public class PricePolicy : IChargingPolicy
{
    public const string BoolParameterName_AllowChargingWhenPriceUnknown = "AllowChargingWhenPriceUnknown";
    public const string IntParameterName_MaxPriceEurocentPerMWh = "MaxPriceEurocentPerMWh";

    private readonly IDatabase _database;
    private readonly ILogger<PricePolicy> _logger;

    public PricePolicy(IDatabase database, ILogger<PricePolicy> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<ChargingPolicyResult> EvaluateAsync(ChargingStationData? chargingStationData, MeterData? meterData)
    {
        string message;
        float currentLimitAmpere;
        var currentPrice = await _database.GetPriceAsync(DateTime.UtcNow);

        // 1. Price is unknown: check relevant setting to determine whether charging is allowed or not.
        if (currentPrice == null)
        {
            if (_database.GetBoolParameterAsync(BoolParameterName_AllowChargingWhenPriceUnknown).Result == true)
            {
                message = "Price is unknown, but current configuration allows charging at unknown price";
                currentLimitAmpere = float.MaxValue;
            }
            else
            {
                message = "Price is unknown, and current configuration does not allow charging at unknown price";
                currentLimitAmpere = 0;
            }
        }
        else // 2. Current price is known: compare to the maximum price setting to determine whether charging is allowed or not.
        {
            var maxPriceEurocentPerMWh = await _database.GetIntParameterAsync(IntParameterName_MaxPriceEurocentPerMWh);

            if (currentPrice.PriceEurocentPerMWh > maxPriceEurocentPerMWh)
            {
                message = $"Price is too high: {currentPrice.PriceEurocentPerMWh} > {maxPriceEurocentPerMWh}";
                currentLimitAmpere = 0;
            }
            else
            {
                message = $"Price is acceptable: {currentPrice.PriceEurocentPerMWh} <= {maxPriceEurocentPerMWh}";
                currentLimitAmpere = float.MaxValue;
            }
        }

        _logger.LogDebug("Price policy result: {currentLimit:f2} ampere, message: {message}", currentLimitAmpere, message);
        return new ChargingPolicyResult
        {
            CurrentLimitAmpere = currentLimitAmpere,
            Message = message
        };
    }
}
