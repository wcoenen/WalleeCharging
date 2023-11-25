using System.Globalization;

namespace WalleeCharging.Price;

public class ElectricityPrice
{
    public ElectricityPrice(DateTime time, int priceEurocentPerMWh)
    {
        if (time.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTimeKind must be UTC");

        Time = time;
        PriceEurocentPerMWh = priceEurocentPerMWh;
    }

    public DateTime Time { get; }
    public int PriceEurocentPerMWh {get; }

    public override string ToString()
    {
        return $"Time={Time:o} Price={PriceEurocentPerMWh}";
    }
}