using Microsoft.Extensions.Logging;
using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;

namespace WalleeCharging.Control;

public class CapacityTariffPolicy : IChargingPolicy
{
    private readonly IDatabase _database;
    private readonly ILogger<CapacityTariffPolicy> _logger;

    public CapacityTariffPolicy(
        IDatabase database,
        ILogger<CapacityTariffPolicy> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<ChargingPolicyResult> EvaluateAsync(ChargingStationData? chargingStationData, MeterData? meterData)
    {
        if (chargingStationData == null || meterData == null)
        {
            throw new ArgumentNullException("ChargingStationData and MeterData are required for CapacityTariffPolicy");
        }

        var maxTotalPowerWatts = await _database.GetParameterAsync("MaxTotalPowerWatts");

        // Account for other consumers and do not exceed MaxTotalPowerWatts
        float non_charger_power = meterData.TotalActivePower - chargingStationData.RealPowerSum;
        float power_available_for_charging = maxTotalPowerWatts - non_charger_power;
        float voltage_sum = meterData.Voltage1 + meterData.Voltage2 + meterData.Voltage3;
        float currentLimit = power_available_for_charging / voltage_sum;

        _logger.LogDebug("Capacity tariff policy result: {currentLimit:f2} ampere", currentLimit);

        return new ChargingPolicyResult
        {
            CurrentLimitAmpere = currentLimit,
            Message = $"Limiting meter power to {maxTotalPowerWatts}W."
        };
    }
}
