public class ApplianceHint
{
    /// <summary>
    /// The name of the appliance and program, e.g. "Dishwasher", "Diswasher Eco", "WashingMachine", ...
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The optimal start time for the appliance program, or null if there is unsufficient price data.
    /// </summary>
    public DateTime? OptimalStartTime { get; set; }

    /// <summary>
    /// The expected total cost for the appliance program in Euro, or null if the optimal start time is unknown.
    /// </summary>
    public decimal? ExpectedTotalCostEuro{ get; set; }
}