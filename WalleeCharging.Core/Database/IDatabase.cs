using WalleeCharging.Price;

namespace WalleeCharging.Database;

public interface IDatabase
{
    Task<int> GetParameterAsync(string name);
    Task SaveParameterAsync(string name, int value);

    Task<ElectricityPrice?> GetPriceAsync(DateTime time);
    IAsyncEnumerable<ElectricityPrice> GetPricesAsync(DateTime timeStart, DateTime timeEnd);
    Task SavePricesAsync(ElectricityPrice[] prices);
}
