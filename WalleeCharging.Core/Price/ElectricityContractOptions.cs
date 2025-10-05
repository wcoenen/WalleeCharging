public class ElectricityContractOptions
{
    public decimal FactorAppliedtoDayAheadPrice { get; set; } = 1;
    public decimal ConstantAddedEuroPerkWh { get; set; } = 0;

    public decimal VATMultiplier { get; set; } = 1.06M;
}