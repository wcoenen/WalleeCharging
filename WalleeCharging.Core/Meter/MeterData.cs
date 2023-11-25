namespace WalleeCharging.Meter;

/// <summary>
/// Power (in watt), voltages (in volt) and currents (in ampere) as reported by the main electricity meter.
/// </summary>
public class MeterData
{
    public float TotalActivePower {get; set; } // does not include reactive load
    public float Current1 {get; set; }
    public float Current2 {get; set; }
    public float Current3 {get; set; }
    public float Voltage1 {get; set; }
    public float Voltage2 {get; set; }
    public float Voltage3 {get; set; }

    public override string ToString()
    {
        return $"i1={Current1:f2} i2={Current2:f2} i3={Current3:f2} v1={Voltage1} v2={Voltage2} v3={Voltage3}";
    }
}
