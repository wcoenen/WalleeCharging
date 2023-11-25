using WalleeCharging.Price;

namespace WalleeCharging.Database;

public interface IDatabase
{
    Task<ChargingControlParameters> GetChargingParametersAsync();
    Task SaveChargingParametersAsync(ChargingControlParameters chargingParameters);

    Task<ElectricityPrice?> GetPriceAsync(DateTime time);
    IAsyncEnumerable<ElectricityPrice> GetPricesAsync(DateTime timeStart, DateTime timeEnd);
    Task SavePricesAsync(ElectricityPrice[] prices);
}
