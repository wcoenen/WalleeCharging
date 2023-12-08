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
        DateTime startTodayUtc = DateTime.Today.ToUniversalTime();
        DateTime endTomorrowUtc = DateTime.Today.AddDays(1).ToUniversalTime();
        await foreach (var price in _database.GetPricesAsync(startTodayUtc, endTomorrowUtc))
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
