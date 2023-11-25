namespace WalleeCharging.Meter;

public interface IMeterDataProvider
{
    Task<MeterData> GetMeterDataAsync();
}
