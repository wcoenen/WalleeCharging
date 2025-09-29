using Microsoft.AspNetCore.Mvc;
using WalleeCharging.Database;
using WalleeCharging.Price;
namespace WalleeCharging.WebApp;

[ApiController]
[Route("api/prices")]
public class PricesController
{
    private readonly ILogger<PricesController> _logger;
    private readonly IDatabase _database;
    
    public PricesController(ILogger<PricesController> logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
        _logger.LogInformation("PricesController created");
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
