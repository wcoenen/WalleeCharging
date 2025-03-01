using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.ChargingStation;
using WalleeCharging.Price;
using WalleeCharging.Control;
using WalleeCharging.WebApp.Services;
using WalleeCharging.WebApp;
using Serilog;
using System.Configuration;

// Bootstrap logger for startup issues
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // paths
    string sqliteFilePath = Path.Combine(
        builder.Environment.ContentRootPath,
        "WalleeCharging.sqlite");

    // Configure Serilog
    builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

    // Configuration values
    var config = builder.Configuration;
    config.AddEnvironmentVariables();
    string alfenEveHostname =   config.GetRequiredValue("AlfenEveHostName");
    string entsoeApiToken =     config.GetRequiredValue("EntsoeApiKey");
    int maxSafeCurrentAmpere =  config.GetRequiredValue<int>("MaxSafeCurrentAmpere");
    int loopDelayMillis =       config.GetRequiredValue<int>("LoopDelayMillis");
    bool shadowMode =           config.GetRequiredValue<bool>("ShadowMode");
    string meterDataSource =    config.GetRequiredValue<string>("MeterDataSource");

    // add meter data source to the container
    if (meterDataSource == "P1")
    {
        builder.Services.AddSingleton<IMeterDataProvider,P1MeterDataProvider>();
    }
    else if (meterDataSource == "HomeWizard")
    {
        string homeWizardUrl = config.GetRequiredValue("HomeWizardApiUrl");
        builder.Services.AddSingleton<IMeterDataProvider>(x =>
            new HomeWizardMeterDataProvider(
                homeWizardUrl,
                x.GetRequiredService<ILogger<HomeWizardMeterDataProvider>>()));
    }
    else
    {
        throw new ConfigurationErrorsException("Unknown MeterDataSource '{meterDataSource}'");
    }

    // Add other services to the container.
    builder.Services.AddRazorPages();
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IDatabase>(x =>
        new SqliteDatabase(
            sqliteFilePath,
            x.GetRequiredService<ILogger<SqliteDatabase>>()));
    builder.Services.AddSingleton<IPriceFetcher>(x => 
        new EntsoePriceFetcher(
            entsoeApiToken,
            x.GetRequiredService<ILogger<EntsoePriceFetcher>>()));
    builder.Services.AddSingleton<INotificationSink,SignalRNotificationSink>();
    builder.Services.AddSingleton<IChargingStation>(x =>
        new AlfenEveModbusChargingStation(
            alfenEveHostname,
            x.GetRequiredService<ILogger<AlfenEveModbusChargingStation>>())
    );

    // ControlLoop background worker
    builder.Services.AddHostedService<ControlLoop>(x => 
        new ControlLoop(
            loopDelayMillis: loopDelayMillis,
            maxSafeCurrentAmpere: maxSafeCurrentAmpere,
            shadowMode: shadowMode,
            x.GetRequiredService<IDatabase>(),
            x.GetRequiredService<IMeterDataProvider>(),
            x.GetRequiredService<IChargingStation>(),
            x.GetRequiredService<INotificationSink>(),
            x.GetRequiredService<ILogger<ControlLoop>>())
    );
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