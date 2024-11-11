namespace WalleeCharging.Price;

public interface IPriceFetcher
{
        /// <summary>
        /// Retrieves electricity prices.
        /// </summary>
        /// <param name="day">The day for which the electricity prices are to be retrieved. Must match the start of a day in the bidding zone.</param>
        /// <param name="cancellationToken"></param>
        Task<ElectricityPrice[]> GetPricesAsync(int year, int month, int day, CancellationToken cancellationToken);
}