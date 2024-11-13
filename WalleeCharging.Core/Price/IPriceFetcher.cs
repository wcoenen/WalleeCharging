namespace WalleeCharging.Price;

public interface IPriceFetcher
{
        /// <summary>
        /// Retrieves electricity prices for the requested day.
        /// </summary>
        Task<ElectricityPrice[]> GetPricesAsync(int year, int month, int day, CancellationToken cancellationToken);
}