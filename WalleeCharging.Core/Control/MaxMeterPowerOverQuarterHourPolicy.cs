using Microsoft.Extensions.Logging;
using WalleeCharging.ChargingStation;
using WalleeCharging.Database;
using WalleeCharging.Meter;

namespace WalleeCharging.Control;

/// <summary>
/// Variant of <see cref="MaxMeterPowerPolicy"/> which interprets the MaxTotalPowerWatts setting
/// as the maximum for the average power over a quarter-hour, as is relevant for the Flanders capacity tariff.
/// </summary>
public class MaxMeterPowerOverQuarterHourPolicy : IChargingPolicy
{
    private const int SNOOZE_DURATION_SECONDS = 180; // 3 minutes, i.e. 20% of a quarter hour
    private readonly IDatabase _database;
    private readonly ILogger<MaxMeterPowerOverQuarterHourPolicy> _logger;
    private Tuple<DateTime,double> _import_at_start_of_quarterhour_kWh;
    private DateTime? _snoozeStartTimeUtc;

    public MaxMeterPowerOverQuarterHourPolicy(
        IDatabase database,
        ILogger<MaxMeterPowerOverQuarterHourPolicy> logger)
    {
        _database = database;
        _logger = logger;
        _import_at_start_of_quarterhour_kWh = Tuple.Create(DateTime.UnixEpoch, (double)0);
    }

    public async Task<ChargingPolicyResult> EvaluateAsync(ChargingStationData? chargingStationData, MeterData? meterData)
    {
        if (chargingStationData == null || meterData == null)
        {
            throw new ArgumentNullException("ChargingStationData and MeterData are required for MaxMeterPowerOverQuarterHourPolicy");
        }

        var maxTotalPowerWatts = await _database.GetParameterAsync("MaxTotalPowerWatts");

        // check for potential fresh start for a new quarter-hour
        DateTime utcNow = DateTime.UtcNow;
        UpdateImportAtStartOfQuarterHour(utcNow, meterData, maxTotalPowerWatts);

        // If snoozing, return early with 0 ampere limit, or end snooze and continue.
        //
        // 6 amps is the minimum which the charging station can communicate to the car,
        // so a limit below 6 amps stops the charging. Stopping charging then
        // leads to much slower depletion of the remaining energy budget, which
        // quickly raises the current limit again. This can lead to twitchy behavior
        // where the charging frequently stops and starts. To mitigate this,
        // we don't charge again until at least SNOOZE_DURATION_SECONDS have elapsed
        // This allows the current limit to recover well above 6 amps before we start
        // charging again.
        if (_snoozeStartTimeUtc.HasValue)
        {
            double secondsSinceSnoozeStart = (utcNow - _snoozeStartTimeUtc.Value).TotalSeconds;
            if (secondsSinceSnoozeStart < SNOOZE_DURATION_SECONDS)
            {
                return new ChargingPolicyResult
                {
                    CurrentLimitAmpere = 0,
                    Message = $"Pausing charging for {SNOOZE_DURATION_SECONDS - secondsSinceSnoozeStart:f0} more seconds (or next quarter-hour) to limit capacity tariff."
                };
            }
            else
            {
                _snoozeStartTimeUtc = null;
            }
        }

        // If we get here, no snooze is active.
        // Calculate current limit from remaining energy budget
        double energyBudget_Joules = maxTotalPowerWatts * 900;
        double consumed_Joules = (meterData.TotalPowerImport - _import_at_start_of_quarterhour_kWh.Item2) * 3_600_000;
        double remaining_Joules = energyBudget_Joules - consumed_Joules;
        double remaining_seconds = _import_at_start_of_quarterhour_kWh.Item1.AddSeconds(900).Subtract(utcNow).TotalSeconds;
        double powerLimit_watt = remaining_Joules / remaining_seconds;
        float voltage_sum = meterData.Voltage1 + meterData.Voltage2 + meterData.Voltage3;
        float currentLimit = (float)(powerLimit_watt / voltage_sum);

        // start snooze if necessary
        if (currentLimit < 6)
        {
            _snoozeStartTimeUtc = utcNow;
            currentLimit = 0;
        }

        return new ChargingPolicyResult
        {
            CurrentLimitAmpere = currentLimit,
            Message = $"Limiting charging power to {powerLimit_watt:f0}W to limit capacity tariff."
        };
    }

    /// <summary>
    /// If _import_at_start_of_quarterhour_kWh is not for the most recent quarter-hour, update it.
    /// </summary>
    private void UpdateImportAtStartOfQuarterHour(DateTime utcNow, MeterData meterData, float maxTotalPowerWatts)
    {
        DateTime mostRecentQuarterHour = GetMostRecentQuarterHourUtc(utcNow);
        if (_import_at_start_of_quarterhour_kWh.Item1 < mostRecentQuarterHour)
        {
            // seconds elapsed since start of most recent quarter hour
            double secondsElapsed = (utcNow - mostRecentQuarterHour).TotalSeconds;

            // estimate of max energy imported since start of most recent quarter hour
            double importCorrection_Joule = secondsElapsed * maxTotalPowerWatts;
            double importCorrection_kWh = importCorrection_Joule / (3_600_000);

            // remember estimated meter data at start of quarter hour
            double import_kWh = meterData.TotalPowerImport - importCorrection_kWh;
            _import_at_start_of_quarterhour_kWh = Tuple.Create(mostRecentQuarterHour, import_kWh);

            // reset snooze state
            _snoozeStartTimeUtc = null;
        }
    }

    private static DateTime GetMostRecentQuarterHourUtc(DateTime utcNow)
    {
        int minute;
        if (utcNow.Minute >= 45)
        {
            minute = 45;
        }
        else if (utcNow.Minute >= 30)
        {
            minute = 30;
        }
        else if (utcNow.Minute >= 15)
        {
            minute = 15;
        }
        else
        {
            minute = 0;
        }
        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, minute, 0, DateTimeKind.Utc);
    }
}
