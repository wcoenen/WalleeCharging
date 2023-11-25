
using WalleeCharging.Price;
using WalleeCharging.Control;

namespace WalleeCharging.WebApp.Services;

/// <summary>
/// ASP.NET Core BackgroundService which just wraps <see cref="ControlLoop"/> and <see cref="PriceFetchingLoop"/>.
/// </summary>
public class BackgroundWorkers : BackgroundService
{
    private readonly ControlLoop _controlLoop;
    private readonly PriceFetchingLoop _priceFetchingLoop;

    public BackgroundWorkers(ControlLoop controlLoop, PriceFetchingLoop priceFetchingLoop)
    {
        _controlLoop = controlLoop;
        _priceFetchingLoop = priceFetchingLoop;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var controlTask = _controlLoop.ExecuteAsync(stoppingToken);
        var priceFetchingTask = _priceFetchingLoop.ExecuteAsync(stoppingToken);
        return Task.WhenAll(controlTask, priceFetchingTask);
    }

}
