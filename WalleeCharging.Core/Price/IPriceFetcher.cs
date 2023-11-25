namespace WalleeCharging.Price;

public interface IPriceFetcher
{
        Task<ElectricityPrice[]> GetPricesAsync(DateTime day, CancellationToken cancellationToken);
}