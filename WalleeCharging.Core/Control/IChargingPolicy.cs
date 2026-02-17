using WalleeCharging.ChargingStation;
using WalleeCharging.Meter;

namespace WalleeCharging.Control;

public interface IChargingPolicy
{
    Task<ChargingPolicyResult> EvaluateAsync(ChargingStationData? chargingStationData, MeterData? meterData);
}

public class ChargingPolicyResult
{
    public float CurrentLimitAmpere { get; set; }
    public string Message { get; set; } = string.Empty;
}
