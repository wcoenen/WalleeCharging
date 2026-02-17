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
    }

    public ChargingControlParameters ChargingParameters { get; set; }

    public async Task OnGetAsync()
    {
        ChargingParameters = new ChargingControlParameters()
        {
            MaxTotalPowerWatts = await _database.GetParameterAsync("MaxTotalPowerWatts"),
            MaxPriceEurocentPerMWh = await _database.GetParameterAsync("MaxPriceEurocentPerMWh")
        };
    }

    public async Task<ActionResult> OnPostAsync(int maxTotalPowerWatts, int maxPriceEurocentPerMWh)
    {
        await _database.SaveParameterAsync("MaxTotalPowerWatts", maxTotalPowerWatts);
        await _database.SaveParameterAsync("MaxPriceEurocentPerMWh", maxPriceEurocentPerMWh);
        return new NoContentResult();
    }
}
