using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WalleeCharging.Price;

namespace WalleeCharging.Database;

public class SqliteDatabase : IDatabase
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDatabase> _logger;

    public SqliteDatabase(IOptions<SqliteDatabaseOptions> options, ILogger<SqliteDatabase> logger)
    {
        string databaseFilePath = options.Value.DatabaseFilePath ?? throw new ArgumentException("Sqlite DatabaseFilePath is not configured");
        _logger = logger;

        _logger.LogInformation("Opening sqlite database '{databaseFilePath}'", databaseFilePath);

        bool newDatabase = !File.Exists(databaseFilePath);
        
        _connectionString = $"Data Source={databaseFilePath}";

        if (newDatabase)
        {
            InitializeDatabaseSchema();
        }
    }

    private void InitializeDatabaseSchema()
    {
        _logger.LogInformation("Initializing new sqlite database");
        // Time is always stored as the number of seconds since 1970-01-01 00:00:00 UTC, aka "unix time".

        // Integer parameters stored as name-value pairs.
        ExecuteNonQuery("CREATE TABLE IntParameters "
            + "(Name TEXT PRIMARY KEY, Value INTEGER)");

        // Price points for the day-ahead market for electricity.
        // Because there has been a transition from 60-minute to 15-minute intervals, we don't make assumptions about interval length.
        // Each price point has a start time (inclusive) and end time (exclusive).
        ExecuteNonQuery("CREATE TABLE DayAheadPrice (StartUnixTime INTEGER, EndUnixTime INTEGER, PriceEurocentPerMWh INTEGER)");
    }

    private void ExecuteNonQuery(string sql)
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using (var command = new SqliteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public async Task<int> GetParameterAsync(string name)
    {
        string sql = "SELECT Value FROM IntParameters WHERE Name = @name";
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("name", name);
                var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return reader.GetInt32(0);
                }
                else
                {
                    return 0; // Return default value if parameter not found
                }
            }
        }
    }

    public async Task SaveParameterAsync(string name, int value)
    {
        string sql = "INSERT OR REPLACE INTO IntParameters(Name, Value) VALUES(@name, @value)";
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("name", name);
                command.Parameters.AddWithValue("value", value);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<ElectricityPrice?> GetPriceAsync(DateTime time)
    {
        if (time.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTimeKind must be UTC");

        string sql = "SELECT StartUnixTime, EndUnixTime, PriceEurocentPerMWh"
            + " FROM DayAheadPrice WHERE StartUnixTime <= @unixTime AND EndUnixTime > @unixTime"; 
        
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("unixTime", DateTimeToUnix(time));
                var reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {
                    await reader.ReadAsync();
                    var price = new ElectricityPrice(
                        UnixToDateTime(reader.GetInt64(0)),
                        UnixToDateTime(reader.GetInt64(1)),
                        reader.GetInt32(2));
                    if (await reader.ReadAsync())
                        throw new InvalidDataException($"Database integrity error: more than one price point found for the same time: {time:o}");
                    return price;
                }
                else
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Gets all prices that are relevant within the given time interval, inclusive timeStart, exclusive timeEnd.
    /// </summary>
    public async IAsyncEnumerable<ElectricityPrice> GetPricesAsync(DateTime timeStart, DateTime timeEnd)
    {
        if ((timeStart.Kind != DateTimeKind.Utc) || (timeEnd.Kind != DateTimeKind.Utc))
        {
            throw new ArgumentException("DateTimeKind must be UTC");
        }

        _logger.LogDebug("querying prices from {timeStart} to {timeEnd}", timeStart, timeEnd);

        string sql = "SELECT StartUnixTime, EndUnixTime, PriceEurocentPerMWh"
            + " FROM DayAheadPrice"
            + " WHERE (@startTime <= StartUnixTime AND  StartUnixTime < @endTime)"
            + " OR (@startTime < EndUnixTime AND  EndUnixTime <= @endTime)"
            + " ORDER BY StartUnixTime ASC";

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using (var command = new SqliteCommand(sql, connection))
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
                            UnixToDateTime(reader.GetInt64(1)),
                            reader.GetInt32(2));
                    }
                }
            }
        }
    }

    public async Task SavePricesAsync(ElectricityPrice[] prices)
    {
        foreach (var price in prices)
        {
            // first check for conflicts
            var conflictingPrice = await GetPricesAsync(price.StartTime, price.EndTime).FirstOrDefaultAsync();
            if (conflictingPrice != null)
            {
                throw new ArgumentException($"Price '{price}' cannot be saved because it conflicts with an existing price: {conflictingPrice}");
            }
            // then save the price in the database
            try
            {
                string sql ="INSERT INTO DayAheadPrice(StartUnixTime, EndUnixTime, PriceEurocentPerMWh) VALUES(@startTime, @endTime, @price);";
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("startTime", DateTimeToUnix(price.StartTime));
                        command.Parameters.AddWithValue("endTime", DateTimeToUnix(price.EndTime));
                        command.Parameters.AddWithValue("price", price.PriceEurocentPerMWh);
                        await command.ExecuteNonQueryAsync();
                    }
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
}
