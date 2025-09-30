using Microsoft.AspNetCore.Mvc;
using WalleeCharging.Database;
using WalleeCharging.Price;
namespace WalleeCharging.WebApp;

[ApiController]
[Route("api/prices")]
public class PricesController
{
    private readonly IDatabase _database;
    
    public PricesController(IDatabase database)
    {
        _database = database;
    }

    [HttpGet]
    public IAsyncEnumerable<ElectricityPrice> Get()
    {
        DateTime now = DateTime.UtcNow;
        // round down to the start of the current quarter-hour
        DateTime currentQuarterHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute-(now.Minute % 15), 0, DateTimeKind.Utc);
        DateTime endTomorrowUtc = DateTime.Today.AddDays(2).ToUniversalTime();
        return _database.GetPricesAsync(currentQuarterHour, endTomorrowUtc);
    }
}
