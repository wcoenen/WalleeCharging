namespace WalleeCharging.ChargingStation;

public interface IChargingStation
{
    /// <summary>
    /// Gets the latest information from the charging station about charging current and power use.
    /// </summary>
    /// <exception cref="ChargingStationException">There was a problem communicating with the charging station.</exception>
    Task<ChargingStationData> GetChargingStationDataAsync();

    /// <summary>
    /// Instructs the charging station to draw no more current than <paramref name="currentLimitAmpere">.
    /// </summary>
    /// <exception cref="ChargingStationException">There was a problem communicating with to the charging station.</exception>
    Task SetCurrentLimitAsync(float currentLimitAmpere);
}
