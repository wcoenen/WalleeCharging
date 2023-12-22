using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.ChargingStation;
using WalleeCharging.Price;
using WalleeCharging.Control;
using WalleeCharging.WebApp.Services;
using WalleeCharging.WebApp;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// paths
string sqliteFilePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "WalleeCharging.sqlite");
string logFilePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "WalleeCharging.log");

// Configure Serilog
builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Configuration values
var config = builder.Configuration;
config.AddEnvironmentVariables();
string alfenEveHostname =   config.GetRequiredValue("AlfenEveHostName");
string homeWizardUrl =      config.GetRequiredValue("HomeWizardApiUrl");
string entsoeApiToken =     config.GetRequiredValue("EntsoeApiKey");
int maxSafeCurrentAmpere =  config.GetRequiredValue<int>("MaxSafeCurrentAmpere");
int loopDelayMillis =       config.GetRequiredValue<int>("LoopDelayMillis");
bool shadowMode =           config.GetRequiredValue<bool>("ShadowMode");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDatabase>(new SqliteDatabase(sqliteFilePath));
builder.Services.AddSingleton<IPriceFetcher>(new EntsoePriceFetcher(entsoeApiToken));
builder.Services.AddSingleton<INotificationSink,SignalRNotificationSink>();
builder.Services.AddSingleton<IMeterDataProvider>(x =>
    new HomeWizardMeterDataProvider(
        homeWizardUrl,
        x.GetRequiredService<ILogger<HomeWizardMeterDataProvider>>()));
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


