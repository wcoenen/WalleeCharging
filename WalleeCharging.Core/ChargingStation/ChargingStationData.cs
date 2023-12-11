namespace WalleeCharging.ChargingStation;

public class ChargingStationData
{
    public float RealPowerSum  {get; set; }
    public float Current1 { get; set; }
    public float Current2 { get; set; }
    public float Current3 { get; set; }

    public override string ToString()
    {
        return $"RealPowerSum={RealPowerSum} Current1={Current1:f2} Current2={Current2:f2} Current3={Current3:f2}";
    }
}
