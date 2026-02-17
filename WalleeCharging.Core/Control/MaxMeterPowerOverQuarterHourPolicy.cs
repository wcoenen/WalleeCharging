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
    private readonly IDatabase _database;
    private readonly ILogger<MaxMeterPowerOverQuarterHourPolicy> _logger;
    private Tuple<DateTime,double> _import_at_start_of_quarterhour_kWh;

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

        DateTime utcNow = DateTime.UtcNow;
        UpdateImportAtStartOfQuarterHour(utcNow, meterData, maxTotalPowerWatts);

        double energyBudget_Joules = maxTotalPowerWatts * 900;
        double consumed_Joules = (meterData.TotalPowerImport - _import_at_start_of_quarterhour_kWh.Item2) * 3_600_000;
        double remaining_Joules = energyBudget_Joules - consumed_Joules;
        double remaining_seconds = _import_at_start_of_quarterhour_kWh.Item1.AddSeconds(900).Subtract(utcNow).TotalSeconds;
        double powerLimit_watt = remaining_Joules / remaining_seconds;
        float voltage_sum = meterData.Voltage1 + meterData.Voltage2 + meterData.Voltage3;
        float currentLimit = (float)(powerLimit_watt / voltage_sum);

        _logger.LogDebug("MaxMeterPowerOverQuarterHourPolicy policy result: {currentLimit:f2} ampere", currentLimit);

        return new ChargingPolicyResult
        {
            CurrentLimitAmpere = currentLimit,
            Message = $"Limiting meter power to {powerLimit_watt:f0}W to stay within quarter-hour budget."
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
