using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WalleeCharging.Price;

namespace WalleeCharging.Database;

public class SqliteDatabase : IDatabase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<SqliteDatabase> _logger;

    public SqliteDatabase(IOptions<SqliteDatabaseOptions> options, ILogger<SqliteDatabase> logger)
    {
        string databaseFilePath = options.Value.DatabaseFilePath ?? throw new ArgumentException("Sqlite DatabaseFilePath is not configured");
        _logger = logger;

        _logger.LogInformation("Opening sqlite database '{databaseFilePath}'", databaseFilePath);

        bool newDatabase = !File.Exists(databaseFilePath);
        
        string connectionString = $"Data Source={databaseFilePath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        if (newDatabase)
        {
            InitializeDatabaseSchema();
        }
    }

    private void InitializeDatabaseSchema()
    {
        _logger.LogInformation("Initializing new sqlite database");
        // Time is always stored as the number of seconds since 1970-01-01 00:00:00 UTC, aka "unix time".

        // Charging parameters. Currently only the newest record is used.
        // (Possible future usecases for multiple records: restoring old settings, or settings that take effect at a certain time.)
        ExecuteNonQuery("CREATE TABLE ChargingParameters "
            + "(UnixTime INTEGER PRIMARY KEY, MaxTotalPowerWatts INTEGER, MaxPriceEurocentPerMWh INTEGER)");
        
        // Price points for the day-ahead market for electricity.
        ExecuteNonQuery("CREATE TABLE DayAheadPrice (UnixTime INTEGER PRIMARY KEY, PriceEurocentPerMWh INTEGER)");
    }

    private void ExecuteNonQuery(string sql)
    {
        using (var command = new SqliteCommand(sql, _connection))
        {
            command.ExecuteNonQuery();
        }
    }

    public async Task<ChargingControlParameters> GetChargingParametersAsync()
    {
        string sql = "SELECT UnixTime, MaxTotalPowerWatts, MaxPriceEurocentPerMWh"
            +" FROM ChargingParameters ORDER BY UnixTime DESC LIMIT 1;";
        using (var command = new SqliteCommand(sql, _connection))
        {
            var reader = await command.ExecuteReaderAsync();
            if (reader.HasRows)
            {
                await reader.ReadAsync();
                return new ChargingControlParameters()
                {
                    MaxTotalPowerWatts = reader.GetInt32(1),
                    MaxPriceEurocentPerMWh = reader.GetInt32(2),
                };
            }
            else
            {
                return new ChargingControlParameters();
            }
        }
    }

    public async Task SaveChargingParametersAsync(ChargingControlParameters chargingParameters)
    {
        string sql ="INSERT INTO ChargingParameters(UnixTime, MaxTotalPowerWatts, MaxPriceEurocentPerMWh) "
            +"VALUES(@time,@power,@price);";
        using (var command = new SqliteCommand(sql, _connection))
        {
            command.Parameters.AddWithValue("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("power", chargingParameters.MaxTotalPowerWatts);
            command.Parameters.AddWithValue("price", chargingParameters.MaxPriceEurocentPerMWh);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<ElectricityPrice?> GetPriceAsync(DateTime time)
    {
        if (time.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTimeKind must be UTC");

        string sql = "SELECT UnixTime, PriceEurocentPerMWh"
            + " FROM DayAheadPrice WHERE UnixTime >= @unixTime ORDER BY UnixTime ASC LIMIT 1";
        using (var command = new SqliteCommand(sql, _connection))
        {
            command.Parameters.AddWithValue("unixTime", DateTimeToUnix(time));
            var reader = await command.ExecuteReaderAsync();
            if (reader.HasRows)
            {
                await reader.ReadAsync();
                return new ElectricityPrice(
                    UnixToDateTime(reader.GetInt64(0)),
                    reader.GetInt32(1));
            }
            else
            {
                return null;
            }
        }
    }

    public async IAsyncEnumerable<ElectricityPrice> GetPricesAsync(DateTime timeStart, DateTime timeEnd)
    {
        if ((timeStart.Kind != DateTimeKind.Utc) || (timeEnd.Kind != DateTimeKind.Utc))
        {
            throw new ArgumentException("DateTimeKind must be UTC");
        }

        string sql = "SELECT UnixTime, PriceEurocentPerMWh"
            +" FROM DayAheadPrice WHERE UnixTime >= @startTime AND UnixTime <= @endTime";

        using (var command = new SqliteCommand(sql, _connection))
        {
            command.Parameters.AddWithValue("startTime", DateTimeToUnix(timeStart));
            command.Parameters.AddWithValue("endTime", DateTimeToUnix(timeEnd));
            var reader = await command.ExecuteReaderAsync();
            var results = new List<ElectricityPrice>();
            if (reader.HasRows)
            {
                while (await reader.ReadAsync())
                {
                    yield return new ElectricityPrice(
                        UnixToDateTime(reader.GetInt64(0)),
                        reader.GetInt32(1));
                }
            }
        }
    }

    public async Task SavePricesAsync(ElectricityPrice[] prices)
    {
        string sql ="INSERT INTO DayAheadPrice(UnixTime, PriceEurocentPerMWh) VALUES(@time,@price);";
        foreach (var price in prices)
        {
            try
            {
                using (var command = new SqliteCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("time", DateTimeToUnix(price.Time));
                    command.Parameters.AddWithValue("price", price.PriceEurocentPerMWh);
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                _logger.LogError("Encountered an error while saving this price in DayAheadPrice table: {price}", price);
                throw;
            }
        }
    }

    private long DateTimeToUnix(DateTime time)
    {
        return time.Subtract(DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond;
    }

    private DateTime UnixToDateTime(long unixTime)
    {
        return DateTime.UnixEpoch.AddTicks(unixTime*TimeSpan.TicksPerSecond);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
