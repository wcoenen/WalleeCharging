using Microsoft.AspNetCore.Http.HttpResults;
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
    public IAsyncEnumerable<ElectricityPrice> Get(DateTime? start, DateTime? end)
    {
        if (start.HasValue && start.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("start time must be in UTC", nameof(start));
        }

        if (end.HasValue && end.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("end time must be in UTC", nameof(end));
        }

        // default values if start or end are not provided
        start ??= DateTime.UtcNow;
        end ??= start.Value.AddHours(36);

        return _database.GetPricesAsync(start.Value, end.Value);
    }
    
    [HttpGet]
    [Route("api/hints")]
    public IAsyncEnumerable<ApplianceHint> GetApplianceHints()
    {
        return _applianceAssistant.GetApplianceHints();
    }

}
