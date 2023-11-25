using Microsoft.AspNetCore.SignalR;
using WalleeCharging.ChargingStation;
using WalleeCharging.Control;
using WalleeCharging.Database;
using WalleeCharging.Price;

namespace WalleeCharging.WebApp.Services;

public class SignalRNotificationSink : INotificationSink
{
    private readonly IHubContext<SignalRHub> _hubContext;

    public SignalRNotificationSink(IHubContext<SignalRHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task Notify(
        ChargingControlParameters chargingControlParameters,
        ElectricityPrice? price,
        ChargingStationData? chargingStationData,
        float currentLimitAmpere,
        string message)
    {
        NoSleep.PreventSleep();

        // see also WalleeCharging.WebApp/wwwroot/js/signalRSubscriber.js
        return _hubContext.Clients.All.SendAsync(
                    "ReceiveControlLoopNotification",
                     $"{DateTime.Now:HH:mm:ss} Limit={currentLimitAmpere:f2}A. {message}");
    }
}
