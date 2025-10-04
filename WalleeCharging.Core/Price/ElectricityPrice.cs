using System.Globalization;

namespace WalleeCharging.Price;

/// <summary>
/// A price for electricity that is valid for a certain time interval.
/// </summary>
public class ElectricityPrice
{
    /// <summary>
    /// Creates a new ElectricityPrice instance.
    /// </summary>
    public ElectricityPrice(DateTime startTime, DateTime endTime, int priceEurocentPerMWh)
    {
        if (startTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("startTime.Kind must be DateTimeKind.UTC");
        if (endTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("endTime.Kind must be DateTimeKind.UTC");
        if (endTime <= startTime)
            throw new ArgumentException("endTime must be after startTime");

        StartTime = startTime;
        EndTime = endTime;
        PriceEurocentPerMWh = priceEurocentPerMWh;
    }

    /// <summary>
    /// The start of the time interval when this price is in effect, inclusive.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// The end of the time interval when this price is in effect, exclusive.
    /// </summary>
    public DateTime EndTime { get; }

    /// <summary>
    /// The price in Eurocent per MWh.
    /// </summary>
    public int PriceEurocentPerMWh { get; }

    public override string ToString()
    {
        return $"StartTime={StartTime:o} EndTime={EndTime:o} Price={PriceEurocentPerMWh}";
    }
}