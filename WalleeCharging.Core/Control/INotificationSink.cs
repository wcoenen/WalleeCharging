using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.Price;

namespace WalleeCharging.Control;

public interface INotificationSink
{
    Task Notify(
        ChargingStationData? chargingStationData,
        MeterData? meterData,
        float currentLimitAmpere,
        string message);
}