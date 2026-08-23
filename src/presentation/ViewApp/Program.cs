using Application;
using CentralServerExchange;
using Configuration;
using Domain.Configuration;
using FrontolDatabase;
using Logger;
using ViewApp.Workers;

if (args.Contains("--help"))
{
    Console.WriteLine("Использование:");
    Console.WriteLine("--service - запуск как дочерний процесс host-службы");
    Console.WriteLine("--help - эта справка");
    return;
}

if (args.Length > 0 && !args.Contains("--service"))
    return;

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