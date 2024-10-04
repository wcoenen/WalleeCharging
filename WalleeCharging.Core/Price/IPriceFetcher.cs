namespace WalleeCharging.Price;

public interface IPriceFetcher
{
        /// <summary>
        /// Retrieves electricity prices.
        /// </summary>
        /// <param name="day">The day </param>
        /// <param name="cancellationToken"></param>
        Task<ElectricityPrice[]> GetPricesAsync(DateTime day, CancellationToken cancellationToken);
}