using Microsoft.Extensions.Options;
using WalleeCharging.Database;
using WalleeCharging.Price;

public class ApplianceAssistant
{
    private readonly ApplianceAssistantOptions _options;
    private readonly ElectricityContractOptions _contractOptions;
    private readonly IDatabase _database;

    public ApplianceAssistant(IOptions<ApplianceAssistantOptions> applianceOptions, IOptions<ElectricityContractOptions> contractOptions,  IDatabase database)
    {
        _options = applianceOptions.Value;
        _contractOptions = contractOptions.Value;
        _database = database;
    }

    public async IAsyncEnumerable<ApplianceHint> GetApplianceHints()
    {
        ElectricityPrice[] prices = await _database.GetPricesAsync(DateTime.UtcNow, DateTime.UtcNow.AddHours(36)).ToArrayAsync();

        foreach (var profile in _options.Profiles)
        {
            yield return GetApplianceHint(profile, prices);
        }
    }

    private ApplianceHint GetApplianceHint(ApplianceProfile profile, ElectricityPrice[] prices)
    {
        // Find the best matching start time for this profile.
        // We do this by checking each possible start time in the price data,
        // and calculating the total cost for the appliance profile.

        // The default answer if we don't have enough data.
        DateTime? optimalStartTime = null;
        decimal? lowestCost = null;
        
        // The available prices and the consumption profile length together limit how many start times we can check.
        int limit = prices.Length - profile.ConsumptionKwhPer15min.Length + 1;

        // But also limit by the configured MaxLookAheadHours.
        limit = Math.Min(limit, _options.MaxLookAheadHours * 4); // 4 price points per hour

        for (int i = 0; i < limit; i++)
        {
            DateTime startTime = prices[i].StartTime;
            decimal totalCostEuro = 0;
            decimal totalConsumptionKwh = 0;
            for (int j = 0; j < profile.ConsumptionKwhPer15min.Length; j++)
            {
                // dvivide price by 100,000 to convert from eurocent per MWh to euro per kWh
                decimal dayAheadPriceEuroPerKWh = prices[i + j].PriceEurocentPerMWh / 100_000M;
                // calculate effective price, which will be higher than day-ahead price due to various taxes and fees
                decimal priceEuroPerkWh = ((dayAheadPriceEuroPerKWh * _contractOptions.FactorAppliedtoDayAheadPrice) + _contractOptions.ConstantAddedEuroPerkWh) * _contractOptions.VATMultiplier;
                totalCostEuro += profile.ConsumptionKwhPer15min[j] * priceEuroPerkWh;
                totalConsumptionKwh += profile.ConsumptionKwhPer15min[j];
            }
            if (lowestCost == null || totalCostEuro < lowestCost)
            {
                lowestCost = totalCostEuro;
                optimalStartTime = startTime;
            }
        }

        return new ApplianceHint
        {
            Name = profile.Name,
            OptimalStartTime = optimalStartTime,
            ExpectedTotalCostEuro = lowestCost
        };
    }
}