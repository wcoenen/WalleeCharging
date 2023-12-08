namespace WalleeCharging.Database;

public class ChargingControlParameters
{
    public int MaxTotalPowerWatts {get; set; }
    public int MaxPriceEurocentPerMWh { get; set; }

    public override string ToString()
    {
        return $"MaxTotalPowerWatts={MaxTotalPowerWatts} MaxPriceEurocentPerMWh={MaxPriceEurocentPerMWh}";
    }
}
