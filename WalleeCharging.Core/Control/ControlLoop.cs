using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.Price;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace WalleeCharging.Control;

public class ControlLoop : BackgroundService
{
    private readonly int _loopDelayMillis;
    private readonly IDatabase _database;
    private readonly IMeterDataProvider _meterDataProvider;
    private readonly IChargingStation _chargingStation;
    private readonly ILogger<ControlLoop> _logger;
    private readonly INotificationSink _notificationSink;
    private readonly bool _shadowMode;
    private readonly IEnumerable<IChargingPolicy> _chargingPolicies;

    public ControlLoop(
        IOptions<ControlLoopOptions> options,
        IDatabase database,
        IMeterDataProvider meterDataProvider,
        IChargingStation chargingStation,
        INotificationSink notificationSink,
        IEnumerable<IChargingPolicy> chargingPolicies,
        ILogger<ControlLoop> logger)
    {
        // settings
        _loopDelayMillis = options.Value.LoopDelayMillis;
        _shadowMode = options.Value.ShadowMode;

        // services
        _database = database;
        _meterDataProvider = meterDataProvider;
        _chargingStation = chargingStation;
        _notificationSink = notificationSink;
        _chargingPolicies = chargingPolicies;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting control loop with delay between iterations of {delay} milliseconds. "
            + "Logging will only happen at the INFO level when the charging current limit changes significantly.",
             _loopDelayMillis);
        
        try
        {
            float previousChargingCurrentLimit = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                float chargingCurrentLimit;
                ChargingStationData? chargingStationData = null;
                MeterData? meterData = null;
                string controlMessage;

                try
                {
                    // Fetch meter and charging station data.
                    // We get the meter data first to sync to fresh output of the P1 port.
                    // (Getting "fresh" data may not be possible for all IMeterDataProvider implementations.)
                    // If the meter and charging station data is not consistent, we read both again.
                    int inconsistentCount = 0;
                    do
                    {
                        meterData = await _meterDataProvider.GetMeterDataAsync();
                        chargingStationData = await _chargingStation.GetChargingStationDataAsync();
                    }
                    while (!IsConsistentData(meterData, chargingStationData) && ++inconsistentCount < 10);

                    if (inconsistentCount >= 10)
                    {
                        throw new InconsistentDataException("Unable to get consistent data from meter and charging station.");
                    }

                    // Evaluate all charging policies and apply the minimum limit.
                    var policyResults = new List<ChargingPolicyResult>();
                    foreach (var policy in _chargingPolicies)
                    {
                        var result = await policy.EvaluateAsync(chargingStationData, meterData);
                        policyResults.Add(result);
                    }

                    // Find the policy with the minimum current limit
                    var limitingPolicy = policyResults.OrderBy(r => r.CurrentLimitAmpere).First();
                    chargingCurrentLimit = limitingPolicy.CurrentLimitAmpere;
                    controlMessage = limitingPolicy.Message;
                }
                catch (Exception e) when (e is ChargingStationException || e is MeterDataException || e is InconsistentDataException)
                {
                    // Something went wrong in the communication with the meter or charging station.
                    // Keeping charging current the same for now.
                    chargingCurrentLimit = previousChargingCurrentLimit;
                    controlMessage = $"Error occurred: {e.Message}";
                    _logger.LogError(e, "Failed to retrieve information in control loop.");
                }

                try
                {
                    if (!_shadowMode)
                    {
                        await _chargingStation.SetCurrentLimitAsync(chargingCurrentLimit);
                    }
                    else
                    {
                        _logger.LogWarning("Running in SHADOW MODE, not sending current limit of {current} ampere", chargingCurrentLimit);
                    }

                    // Fetch parameters and price for logging
                    var chargingParameters = new ChargingControlParameters()
                    {
                        MaxTotalPowerWatts = await _database.GetParameterAsync("MaxTotalPowerWatts"),
                        MaxPriceEurocentPerMWh = await _database.GetParameterAsync("MaxPriceEurocentPerMWh")
                    };
                    var currentPrice = await _database.GetPriceAsync(DateTime.UtcNow);

                    // log this control loop iteration
                    await LogAndNotify(
                        previousChargingCurrentLimit,
                        chargingCurrentLimit,
                        chargingStationData,
                        meterData,
                        controlMessage,
                        chargingParameters,
                        currentPrice?.PriceEurocentPerMWh);
                }
                catch (ChargingStationException e)
                {
                    // Something went wrong in the communication with the charging station.
                    // All we can do is try again in the next iteration.
                    _logger.LogError(e, "Failed to send current limit to charging station.");
                }

                // Remember charging current limit setpoint for next iteration.
                previousChargingCurrentLimit = chargingCurrentLimit;
                
                // wait until next iteration of the control loop
                await Task.Delay(_loopDelayMillis, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            // normal exit via stoppingToken during Task.Delay
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Exiting control loop because of unexpected exception.");
            throw;
        }
        _logger.LogInformation("Exiting control loop.");
    }

    private bool IsConsistentData(MeterData meterData, ChargingStationData chargingStationData)
    {
        if (meterData.TotalActivePower < chargingStationData.RealPowerSum)
        {
            _logger.LogWarning("Meter data is not showing (all) the power use reported by the charging station.\n"
                    +"This data will be ignored and fresh data will be fetched.\n"
                    +"Meter data: {meterData}\n"
                    +"Charging Station data: {chargingStationData}",
                meterData,
                chargingStationData);
            return false;
        }
        return true;
    }

    private async Task LogAndNotify(
        float previousChargingCurrentLimit,
        float chargingCurrentLimit,  
        ChargingStationData? chargingStationData,
        MeterData? meterData,
        string controlMessage,
        ChargingControlParameters chargingParameters,
        int? currentPrice)
    {
        // Determine log level.
        // Only log at the Information level if the charging current limit is changing by at least 10%.
        // Otherwise, log at the debug level.
        LogLevel controlLoopIterationLogLevel;
        float relativeChange = Math.Abs(chargingCurrentLimit - previousChargingCurrentLimit) / previousChargingCurrentLimit;
        if (relativeChange > 0.1)
        {
            controlLoopIterationLogLevel = LogLevel.Information;
        }
        else
        {
            controlLoopIterationLogLevel = LogLevel.Debug;
        }

        _logger.Log(
            controlLoopIterationLogLevel,
            "Control loop update.\n"
                + "- charging parameters: {parameters}\n"
                + "- current price: {price}\n"
                + "- charging station data: {chargingStationData}\n"
                + "- meterData: {meterData}\n"
                + "- next current limit setpoint: {currentLimit}\n"
                + "- control message: {message}",
            chargingParameters,
            currentPrice,
            chargingStationData,
            meterData,
            chargingCurrentLimit,
            controlMessage);

        // notify users
        await _notificationSink.Notify(
            chargingParameters,
            currentPrice,
            chargingStationData,
            meterData,
            chargingCurrentLimit,
            controlMessage);
    }


}
