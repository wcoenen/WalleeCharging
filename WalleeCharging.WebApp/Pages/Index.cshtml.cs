using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WalleeCharging.Database;
using WalleeCharging.Price;

namespace WalleeCharging.WebApp.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _database;

    public IndexModel(ILogger<IndexModel> logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
        ChargingParameters = new ChargingControlParameters();
        Prices = new ElectricityPrice[0];
    }

    public ChargingControlParameters ChargingParameters { get; set; }
    public IEnumerable<ElectricityPrice> Prices {get; set; }

    public async Task OnGetAsync()
    {
        ChargingParameters = await _database.GetChargingParametersAsync();

        var prices = new List<ElectricityPrice>();
        var now = DateTime.UtcNow;
        // round down to the start of the current quarter-hour
        DateTime currentQuarterHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute-(now.Minute % 15), 0, DateTimeKind.Utc);
        DateTime endTomorrowUtc = DateTime.Today.AddDays(2).ToUniversalTime();
        await foreach (var price in _database.GetPricesAsync(currentQuarterHour, endTomorrowUtc))
        {
            prices.Add(price);
        }
        Prices = prices;
    }

    public async Task<ActionResult> OnPostAsync(int maxTotalPowerWatts, int maxPriceEurocentPerMWh)
    {
        await _database.SaveChargingParametersAsync(
            new ChargingControlParameters()
            {
                MaxTotalPowerWatts = maxTotalPowerWatts,
                MaxPriceEurocentPerMWh = maxPriceEurocentPerMWh
            }
        );
        return new NoContentResult();
    }
}
