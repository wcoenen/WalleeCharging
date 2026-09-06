using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WalleeCharging.Database;
using WalleeCharging.Price;
using WalleeCharging.Control;

namespace WalleeCharging.WebApp.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _database;

    public IndexModel(ILogger<IndexModel> logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
    }

    [BindProperty]
    public int MaxTotalPowerWatts { get; set; }

    [BindProperty]
    public int MaxPriceEurocentPerMWh { get; set; }

    [BindProperty]
    public bool AllowChargingWhenPriceUnknown { get; set; }

    public async Task OnGetAsync()
    {
        MaxTotalPowerWatts = await _database.GetIntParameterAsync(MaxMeterPowerOverQuarterHourPolicy.IntParameterName_MaxTotalPowerWatts) ?? 0;
        MaxPriceEurocentPerMWh = await _database.GetIntParameterAsync(PricePolicy.IntParameterName_MaxPriceEurocentPerMWh) ?? 0;
        AllowChargingWhenPriceUnknown = await _database.GetBoolParameterAsync(PricePolicy.BoolParameterName_AllowChargingWhenPriceUnknown) ?? false;
    }

    public async Task<ActionResult> OnPostAsync()
    {
        _logger.LogDebug($"Received POST request with MaxTotalPowerWatts={MaxTotalPowerWatts}, MaxPriceEurocentPerMWh={MaxPriceEurocentPerMWh}, AllowChargingWhenPriceUnknown={AllowChargingWhenPriceUnknown}");

        await _database.SaveIntParameterAsync(MaxMeterPowerOverQuarterHourPolicy.IntParameterName_MaxTotalPowerWatts, MaxTotalPowerWatts);
        await _database.SaveIntParameterAsync(PricePolicy.IntParameterName_MaxPriceEurocentPerMWh, MaxPriceEurocentPerMWh);
        await _database.SaveBoolParameterAsync(PricePolicy.BoolParameterName_AllowChargingWhenPriceUnknown, AllowChargingWhenPriceUnknown);

        return new NoContentResult();
    }
}
