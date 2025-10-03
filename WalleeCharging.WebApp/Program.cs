using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.ChargingStation;
using WalleeCharging.Price;
using WalleeCharging.Control;
using WalleeCharging.WebApp.Services;
using WalleeCharging.WebApp;
using Serilog;
using System.Configuration;
using System.Diagnostics;

// Bootstrap logger for startup issues
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    if (builder.Environment.IsDevelopment())
    {
        // The ENTSOE API key can be stored in the user secrets under the name "ENTSOE:ApiKey"
        builder.Configuration.AddUserSecrets<Program>();
    }

    // paths
    string sqliteFilePath = Path.Combine(
        builder.Environment.ContentRootPath,
        "WalleeCharging.sqlite");

    // Configure Serilog
    builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

    // Configuration values
    var config = builder.Configuration;
    config.AddEnvironmentVariables();
    string meterDataSource =    config.GetRequiredValue<string>("MeterDataSource");

    // add meter data source to the container
    if (meterDataSource == "P1")
    {
        builder.Services.AddSingleton<IMeterDataProvider,P1MeterDataProvider>();
    }
    else if (meterDataSource == "HomeWizard")
    {
        builder.Services.Configure<HomeWizardOptions>(config.GetSection("HomeWizard"));
        builder.Services.AddSingleton<IMeterDataProvider, HomeWizardMeterDataProvider>();
    }
    else
    {
        throw new ConfigurationErrorsException("Unknown MeterDataSource '{meterDataSource}'");
    }

    // Add other services to the container.
    builder.Services.AddRazorPages();
    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IDatabase>(x =>
        new SqliteDatabase(
            sqliteFilePath,
            x.GetRequiredService<ILogger<SqliteDatabase>>()));



    builder.Services.AddSingleton<INotificationSink,SignalRNotificationSink>();

    // Price fetching via Entsoe
    builder.Services.Configure<EntsoeOptions>(config.GetSection("ENTSOE"));
    builder.Services.AddSingleton<IPriceFetcher, EntsoePriceFetcher>();

    // Alfen Eve charging station via modbus TCP
    builder.Services.Configure<AlfenEveOptions>(config.GetSection("AlfenEve"));
    builder.Services.AddSingleton<IChargingStation, AlfenEveModbusChargingStation>();

    // ControlLoop background worker
    builder.Services.Configure<ControlLoopOptions>(config.GetSection("ControlLoop"));
    builder.Services.AddHostedService<ControlLoop>();

    // PriceFetchingLoop background worker
    builder.Services.AddHostedService<PriceFetchingLoop>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthorization();

    app.MapRazorPages();
    app.MapControllers();

    // see also WalleeCharging.WebApp/wwwroot/js/signalr-subscriber.js
    app.MapHub<SignalRHub>("/signalr");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WalleeCharging startup failed!");
}
finally
{
    Log.CloseAndFlush();
}