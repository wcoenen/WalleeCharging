using Microsoft.Extensions.Options;
using WalleeCharging.Database;
using WalleeCharging.Price;

public class ApplianceAssistant
{
    private readonly ApplianceProfile[] _profiles;
    private readonly IDatabase _database;

    public ApplianceAssistant(IOptions<ApplianceAssistantOptions> options, IDatabase database)
    {
        _profiles = options.Value.Profiles;
        _database = database;
    }

    public async IAsyncEnumerable<ApplianceHint> GetApplianceHints()
    {
        ElectricityPrice[] prices = await _database.GetPricesAsync(DateTime.UtcNow, DateTime.UtcNow.AddHours(36)).ToArrayAsync();

        foreach (var profile in _profiles)
        {
            yield return GetApplianceHint(profile, prices);
        }
    }

    private static ApplianceHint GetApplianceHint(ApplianceProfile profile, ElectricityPrice[] prices)
    {
        // Find the best matching start time for this profile.
        // We do this by checking each possible start time in the price data,
        // and calculating the total cost for the appliance profile.
        DateTime? optimalStartTime = null;
        decimal? lowestCost = null;
        for (int i = 0; i < prices.Length; i++)
        {
            DateTime startTime = prices[i].StartTime;
            decimal totalCost = 0;
            decimal totalConsumptionKwh = 0;
            bool profileFits = (prices.Length - i) >= profile.ConsumptionKwhPer15min.Length;
            if (!profileFits)
            {
                break;
            }
            for (int j = 0; j < profile.ConsumptionKwhPer15min.Length; j++)
            {
                totalCost += profile.ConsumptionKwhPer15min[j] * prices[i + j].PriceEurocentPerMWh / 100_000; // convert from eurocent per MWh to euro per kWh
                totalConsumptionKwh += profile.ConsumptionKwhPer15min[j];
            }
            if (lowestCost == null || totalCost < lowestCost)
            {
                lowestCost = totalCost;
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