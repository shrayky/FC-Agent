using Application;
using CentralServerExchange;
using Configuration;
using Domain.Configuration;
using FrontolDatabase;
using Logger;
using ViewApp.Services;
using ViewApp.Workers;

const int ipPort = 2587;

if (args.Length > 0)
{
    StartService.ProcessStartWithArguments(args, ipPort);
    return;
}

var settingsLoadResult = await ParametersLoader.LoadFromAppFolder();
Parameters appSettings = new();

if (settingsLoadResult.IsSuccess)                                   
    appSettings = settingsLoadResult.Value;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls($"http://0.0.0.0:{appSettings.ServerSettings.ApiIpPort}");

builder.Services.AddMemoryCache();
builder.Services.AddConfigurationServices();
builder.Services.AddConfigureLogger(appSettings.LoggerSettings);
builder.Services.AddApplicationServices();
builder.Services.AddCentralServerClient();
builder.Services.AddFrontolDatabase(appSettings.DatabaseConnection);
builder.Services.AddHostedService<AfterStartWorker>();

// Добавить Razor Pages вместо Blazor
builder.Services.AddRazorPages();

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();

await app.RunAsync();