using WalleeCharging.Price;

namespace WalleeCharging.Database;

public interface IDatabase
{
    Task<int?> GetIntParameterAsync(string name);
    Task SaveIntParameterAsync(string name, int value);

    Task<bool?> GetBoolParameterAsync(string name);
    Task SaveBoolParameterAsync(string name, bool value);

    Task<ElectricityPrice?> GetPriceAsync(DateTime time);
    IAsyncEnumerable<ElectricityPrice> GetPricesAsync(DateTime timeStart, DateTime timeEnd);
    Task SavePricesAsync(ElectricityPrice[] prices);
}
