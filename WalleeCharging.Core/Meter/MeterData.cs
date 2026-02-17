namespace WalleeCharging.Meter;

/// <summary>
/// Power (in watt), voltages (in volt) and currents (in ampere) as reported by the main electricity meter.
/// </summary>
public class MeterData
{
    public float TotalPowerImport => TotalPowerImportT1 + TotalPowerImportT2;
    public float TotalPowerImportT1 { get; set; }
    public float TotalPowerImportT2 { get; set; }
    public float TotalActivePower {get; set; } // does not include reactive load
    public float Current1 {get; set; }
    public float Current2 {get; set; }
    public float Current3 {get; set; }
    public float Voltage1 {get; set; }
    public float Voltage2 {get; set; }
    public float Voltage3 {get; set; }

    public override string ToString()
    {
        return $"TotalPowerImport={TotalPowerImport:f3}kWh TotalActivePower={TotalActivePower:f0}w i1={Current1:f2}A i2={Current2:f2}A i3={Current3:f2}A v1={Voltage1}V v2={Voltage2}V v3={Voltage3}V";
    }
}
