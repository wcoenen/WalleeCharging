using Microsoft.AspNetCore.Mvc;
using WalleeCharging.Database;
using WalleeCharging.Price;
namespace WalleeCharging.WebApp;

[ApiController]
public class ApiController
{
    private readonly IDatabase _database;
    private readonly ApplianceAssistant _applianceAssistant;

    public ApiController(IDatabase database, ApplianceAssistant applianceAssistant)
    {
        _database = database;
        _applianceAssistant = applianceAssistant;
    }

    [HttpGet]
    [Route("api/prices")]
    public IAsyncEnumerable<ElectricityPrice> Get()
    {
        DateTime now = DateTime.UtcNow;
        // round down to the start of the current quarter-hour
        DateTime currentQuarterHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute - (now.Minute % 15), 0, DateTimeKind.Utc);
        DateTime endTomorrowUtc = DateTime.Today.AddDays(2).ToUniversalTime();
        return _database.GetPricesAsync(currentQuarterHour, endTomorrowUtc);
    }
    
    [HttpGet]
    [Route("api/hints")]
    public IAsyncEnumerable<ApplianceHint> GetApplianceHints()
    {
        return _applianceAssistant.GetApplianceHints();
    }

}
