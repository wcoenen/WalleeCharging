using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Price;

namespace WalleeCharging.Control;

public interface INotificationSink
{
    Task Notify(
        ChargingControlParameters chargingControlParameters,
        ElectricityPrice? price,
        ChargingStationData? chargingStationData,
        float currentLimitAmpere,
        string message);
}