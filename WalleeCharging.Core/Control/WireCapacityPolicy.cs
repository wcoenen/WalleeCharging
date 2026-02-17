using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WalleeCharging.ChargingStation;
using WalleeCharging.Meter;

namespace WalleeCharging.Control;

public class WireCapacityPolicy : IChargingPolicy
{
    private readonly int _maxSafeCurrentAmpere;
    private readonly ILogger<WireCapacityPolicy> _logger;

    public WireCapacityPolicy(
        IOptions<ControlLoopOptions> options,
        ILogger<WireCapacityPolicy> logger)
    {
        _maxSafeCurrentAmpere = options.Value.MaxSafeCurrentAmpere;
        _logger = logger;
    }

    public Task<ChargingPolicyResult> EvaluateAsync(ChargingStationData? chargingStationData, MeterData? meterData)
    {
        if (chargingStationData == null || meterData == null)
        {
            throw new ArgumentNullException("ChargingStationData and MeterData are required for WireCapacityPolicy");
        }

        // Account for other consumers and do not exceed MaxPhaseCurrentAmpere.
        // To be conservative and to avoid having to map the phases in the reported meter data versus charger data,
        // we assume the smallest charging current is currently being drawn from all 3 phases. This slightly
        // overestimates the non-charger loads.
        // (Note that for one phase charging this doesn't work; smallest_charging_current would be zero.)
        float smallest_charging_current = Math.Min(
            Math.Min(chargingStationData.Current1, chargingStationData.Current2),
            chargingStationData.Current3);
        float current_non_charger_1 = meterData.Current1 - smallest_charging_current;
        float current_non_charger_2 = meterData.Current2 - smallest_charging_current;
        float current_non_charger_3 = meterData.Current3 - smallest_charging_current;
        float wire_capacity_available_1 = _maxSafeCurrentAmpere - current_non_charger_1;
        float wire_capacity_available_2 = _maxSafeCurrentAmpere - current_non_charger_2;
        float wire_capacity_available_3 = _maxSafeCurrentAmpere - current_non_charger_3;
        float currentLimit = Math.Min(
            Math.Min(wire_capacity_available_1, wire_capacity_available_2),
            wire_capacity_available_3);

        _logger.LogDebug("Wire capacity policy result: {currentLimit:f2} ampere", currentLimit);

        return Task.FromResult(new ChargingPolicyResult
        {
            CurrentLimitAmpere = currentLimit,
            Message = $"Limiting meter current to {_maxSafeCurrentAmpere}A."
        });
    }
}
