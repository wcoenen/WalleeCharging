using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.Price;

namespace WalleeCharging.Control;

public interface INotificationSink
{
    Task Notify(
        ChargingControlParameters chargingControlParameters,
        ElectricityPrice? price,
        ChargingStationData? chargingStationData,
        MeterData? meterData,
        float currentLimitAmpere,
        string message);
}