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
using Microsoft.VisualBasic;

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

    // Add meter data source to the container.
    // Both a serial port connection to the P1 port, and the HTTP API of the HomeWizard P1 meter are supported.
    //
    // If both are configured the HomeWizard API is preferred.
    // This makes it easier to debug on a dev machine which is not directly connected to a P1 port.
    // In that context, appsettings.Development.json and appsettings.json will be merged, possibly
    // causing both sections to be present.
    if (config.GetSection("HomeWizard").Exists())
    {
        builder.Services.Configure<HomeWizardOptions>(config.GetSection("HomeWizard"));
        builder.Services.AddSingleton<IMeterDataProvider, HomeWizardMeterDataProvider>();
    }
    else if (config.GetSection("P1Meter").Exists())
    {
        builder.Services.Configure<P1MeterOptions>(config.GetSection("P1Meter"));
        builder.Services.AddSingleton<IMeterDataProvider, P1MeterDataProvider>();
    }
    else
    {
        throw new ConfigurationErrorsException("No P1Meter or HomeWizard configuration found. Please configure one of these meter data sources.");
    }

    // Add other services to the container.
    builder.Services.AddRazorPages();
    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<INotificationSink,SignalRNotificationSink>();

    // Sqlite database
    builder.Services.Configure<SqliteDatabaseOptions>(config.GetSection("Sqlite"));
    builder.Services.AddSingleton<IDatabase,SqliteDatabase>();

    // Price fetching via Entsoe
    builder.Services.Configure<EntsoeOptions>(config.GetSection("ENTSOE"));
    builder.Services.AddSingleton<IPriceFetcher, EntsoePriceFetcher>();

    // Alfen Eve charging station via modbus TCP
    builder.Services.Configure<AlfenEveOptions>(config.GetSection("AlfenEve"));
    builder.Services.AddSingleton<IChargingStation, AlfenEveModbusChargingStation>();

    // Charging policies
    builder.Services.AddSingleton<IChargingPolicy, PricePolicy>();
    builder.Services.AddSingleton<IChargingPolicy, WireCapacityPolicy>();
    builder.Services.AddSingleton<IChargingPolicy, CapacityTariffPolicy>();

    // ControlLoop background worker
    builder.Services.Configure<ControlLoopOptions>(config.GetSection("ControlLoop"));
    builder.Services.AddHostedService<ControlLoop>();

    // PriceFetchingLoop background worker
    builder.Services.AddHostedService<PriceFetchingLoop>();

    // Appliance assistant
    builder.Services.Configure<ApplianceAssistantOptions>(config.GetSection("ApplianceAssistant"));
    builder.Services.Configure<ElectricityContractOptions>(config.GetSection("ElectricityContract"));
    builder.Services.AddSingleton<ApplianceAssistant>();

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