using System.Globalization;
using WalleeCharging.Database;
using WalleeCharging.Meter;
using WalleeCharging.ChargingStation;
using WalleeCharging.Price;
using WalleeCharging.Control;
using WalleeCharging.WebApp.Services;
using WalleeCharging.WebApp;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

string sqliteFilePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "WalleeCharging.sqlite");

// Configuration values
string alfenEveHostname = builder.Configuration.GetRequiredValue("AlfenEveHostName");
string homeWizardUrl = builder.Configuration.GetRequiredValue("HomeWizardApiUrl");
string entsoeApiToken = builder.Configuration.GetRequiredValue("EntsoeApiKey");
int maxSafeCurrentAmpere = Int32.Parse(
    builder.Configuration.GetRequiredValue("MaxSafeCurrentAmpere"),
    CultureInfo.InvariantCulture);
int loopDelayMillis = Int32.Parse(
    builder.Configuration.GetRequiredValue("LoopDelayMillis"),
    CultureInfo.InvariantCulture);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDatabase>(new SqliteDatabase(sqliteFilePath));
builder.Services.AddSingleton<IMeterDataProvider>(new HomeWizardMeterDataProvider(homeWizardUrl));
builder.Services.AddSingleton<IChargingStation>(new AlfenEveModbusChargingStation(alfenEveHostname));
builder.Services.AddSingleton<IPriceFetcher>(new EntsoePriceFetcher(entsoeApiToken));
builder.Services.AddSingleton<PriceFetchingLoop>();
builder.Services.AddSingleton<INotificationSink,SignalRNotificationSink>();
builder.Services.AddSingleton(x => 
    new ControlLoop(
        loopDelayMillis,
        maxSafeCurrentAmpere,
        x.GetRequiredService<IDatabase>(),
        x.GetRequiredService<IMeterDataProvider>(),
        x.GetRequiredService<IChargingStation>(),
        x.GetRequiredService<INotificationSink>(),
        x.GetRequiredService<ILogger<ControlLoop>>())
);
builder.Services.AddHostedService<BackgroundWorkers>();

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
