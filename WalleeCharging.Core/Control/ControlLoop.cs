using System.Diagnostics;
using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.Price;
using Microsoft.Extensions.Logging;

namespace WalleeCharging.Control;

public class ControlLoop
{
    private readonly int _loopDelayMillis;
    private readonly int _maxSafeCurrentAmpere;
    private readonly IDatabase _database;
    private readonly IMeterDataProvider _meterDataProvider;
    private readonly IChargingStation _chargingStation;
    private readonly ILogger<ControlLoop> _logger;
    private readonly INotificationSink _notificationSink;
    private readonly bool _shadowMode;

    public ControlLoop(
        int loopDelayMillis,
        int maxSafeCurrentAmpere,
        bool shadowMode,
        IDatabase database,
        IMeterDataProvider meterDataProvider,
        IChargingStation chargingStation,
        INotificationSink notificationSink,
        ILogger<ControlLoop> logger)
    {
        // settings
        _loopDelayMillis = loopDelayMillis;
        _maxSafeCurrentAmpere = maxSafeCurrentAmpere;
        _shadowMode = shadowMode;

        // services
        _database = database;
        _meterDataProvider = meterDataProvider;
        _chargingStation = chargingStation;
        _notificationSink = notificationSink;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
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
                
                // Fetch data from database and check price constraint. This should be quick, so we do that first.
                var chargingParameters = await _database.GetChargingParametersAsync();
                var currentPrice = await _database.GetPriceAsync(DateTime.UtcNow);
                if (!IsPriceAcceptable(chargingParameters, currentPrice))
                {
                    chargingCurrentLimit = 0;
                    if (currentPrice == null)
                    {
                        controlMessage = $"Price is unknown.";
                    }
                    else
                    {
                        controlMessage = $"Price is too high: {currentPrice.PriceEurocentPerMWh} > {chargingParameters.MaxPriceEurocentPerMWh}";
                    }
                }
                else
                {
                    try
                    {
                        // Price is acceptable.
                        // The next checks require data from the meter and charging station.
                        // Don't "await" yet; fetch data in parallel.
                        var meterDataTask = _meterDataProvider.GetMeterDataAsync();
                        var chargingStationDataTask = _chargingStation.GetChargingStationDataAsync();

                        // Make sure all tasks have completed.
                        meterData = await meterDataTask;
                        chargingStationData = await chargingStationDataTask;

                        // Calculate both constraints and apply the smaller result.
                        float charging_current_constraint1 = GetMaxCurrentWire(chargingParameters, meterData, chargingStationData);
                        float charging_current_constraint2 = GetMaxCurrentCapacityTarif(chargingParameters, meterData, chargingStationData);
                        if (charging_current_constraint1 <= charging_current_constraint2)
                        {
                            chargingCurrentLimit = charging_current_constraint1;
                            controlMessage = $"Limiting meter current to {_maxSafeCurrentAmpere}A.";
                        }
                        else
                        {
                            chargingCurrentLimit = charging_current_constraint2;
                            controlMessage = $"Limiting meter power to {chargingParameters.MaxTotalPowerWatts}W.";
                        }

                    }
                    catch (Exception e) when (e is ChargingStationException || e is MeterDataException)
                    {
                        // Something went wrong in the communication with the meter or charging station.
                        // Disable charging for now.
                        chargingCurrentLimit = 0;
                        controlMessage = $"Error occurred: {e.Message}";
                        _logger.LogError(e, "Failed to retrieve information in control loop.");
                    }
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
        catch (Exception e)
        {
            _logger.LogCritical(e, "Exiting control loop because of unexpected exception.");
            throw;
        }
        _logger.LogInformation("Exiting control loop.");
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

    private float GetMaxCurrentCapacityTarif(ChargingControlParameters chargingParameters, MeterData meterData, ChargingStationData chargingStationData)
    {
        // current constraint 2: account for other consumers and do not exceed MaxTotalPowerWatts
        float non_charger_power = meterData.TotalActivePower - chargingStationData.RealPowerSum;
        if (non_charger_power < 0)
        {
            // This can only mean the meter data is out of date and not yet accounting for the charging power.
            // Log a warning and assume meterData.TotalActivePower is all from other consumers.
            _logger.LogWarning("Meter data is not yet showing charging, GetMaxCurrentCapacityTarif result may be unreliable!");
            non_charger_power = meterData.TotalActivePower;
        }
        float power_available_for_charging = chargingParameters.MaxTotalPowerWatts - non_charger_power;
        float voltage_sum = meterData.Voltage1 + meterData.Voltage2 + meterData.Voltage3;
        float charging_current_constraint2 = power_available_for_charging / voltage_sum;
        _logger.LogDebug($"GetMaxCurrentCapacityTarif result: {charging_current_constraint2:f2} ampere");
        return charging_current_constraint2;
    }

    private float GetMaxCurrentWire(ChargingControlParameters chargingParameters, MeterData meterData, ChargingStationData chargingStationData)
    {
        // current constraint 1: account for other consumers and do not exceed MaxPhaseCurrentAmpere
        // To be conservative and to avoid having to map the phases in the reported meter data versus charger data,
        // we assume the smallest charging current is currently being drawn from all 3 phases. This slightly
        // overestimates the non-charger loads. 
        // (Note that for one phase charging this doesn't work; smallest_charging_current would be zero.)
        float smallest_charging_current = Math.Min(
            Math.Min(chargingStationData.Current1, chargingStationData.Current2),
            chargingStationData.Current3);
        float current_non_charger_1 = meterData.Current1 - smallest_charging_current;
        float current_non_charger_2 = meterData.Current2 - smallest_charging_current;
        float current_non_charger_3 = meterData.Current3 - smallest_charging_current;
        float wire_capacity_available_1 = _maxSafeCurrentAmpere - current_non_charger_1;
        float wire_capacity_available_2 = _maxSafeCurrentAmpere - current_non_charger_2;
        float wire_capacity_available_3 = _maxSafeCurrentAmpere - current_non_charger_3;
        float charging_current_constraint1 = Math.Min(
            Math.Min(wire_capacity_available_1, wire_capacity_available_2),
            wire_capacity_available_3);
        _logger.LogDebug($"GetMaxCurrentWire result: {charging_current_constraint1:f2} ampere");
        return charging_current_constraint1;
    }

    private bool IsPriceAcceptable(ChargingControlParameters chargingParameters, ElectricityPrice? currentPrice)
    {
        bool result = currentPrice != null 
            && currentPrice.PriceEurocentPerMWh <= chargingParameters.MaxPriceEurocentPerMWh;
        _logger.LogDebug("IsPriceAcceptable result: {result}", result);
        return result;
    }


}
