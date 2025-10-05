public class ApplianceProfile
{
    /// <summary>
    /// The name of the appliance and program, e.g. "Dishwasher", "Diswasher Eco", "WashingMachine", ...
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A sequence of consumption values in kWh for each 15-minute interval.
    /// </summary>
    public decimal[] ConsumptionKwhPer15min { get; set; } = Array.Empty<decimal>();
}