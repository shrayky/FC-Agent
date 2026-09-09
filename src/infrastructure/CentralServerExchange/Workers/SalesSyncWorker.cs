using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Frontol.Interfaces;
using Domain.Sales.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Workers;

public class SalesSyncWorker : BackgroundService
{
    private const int BatchLimit = 50;

    private readonly ILogger<SalesSyncWorker> _logger;
    private readonly SignalRAgentClient _signalRClient;
    private readonly IParametersService _parametersService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISalesCursorState _cursor;

    public SalesSyncWorker(
        ILogger<SalesSyncWorker> logger,
        SignalRAgentClient signalRClient,
        IParametersService parametersService,
        IServiceScopeFactory scopeFactory,
        ISalesCursorState cursor)
    {
        _logger = logger;
        _signalRClient = signalRClient;
        _parametersService = parametersService;
        _scopeFactory = scopeFactory;
        _cursor = cursor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
#else
        await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken);
#endif

        while (!stoppingToken.IsCancellationRequested)
        {
            var delaySeconds = 60;

            try
            {
                delaySeconds = await Tick();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка цикла сбора продаж");
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task<int> Tick()
    {
        var settings = await _parametersService.Current();
        var sales = settings.SalesSettings ?? new SalesSettings();
        var delaySeconds = PollSeconds(sales.PollIntervalSeconds);

        if (!sales.LoadSales)
            return delaySeconds;

        if (!_signalRClient.ConnectionUp())
            return delaySeconds;

        if (!_cursor.Initialized)
        {
            await _signalRClient.RequestSalesCursor();
            return delaySeconds;
        }

        using var scope = _scopeFactory.CreateScope();
        var documents = scope.ServiceProvider.GetRequiredService<IFrontolSalesDocuments>();
        var loaded = await documents.After(_cursor.LastDocumentNumber, BatchLimit);

        if (loaded.IsFailure)
        {
            _logger.LogError(loaded.Error);
            return delaySeconds;
        }

        if (loaded.Value.Count == 0)
            return delaySeconds;

        var sent = await _signalRClient.SendSalesDocuments(loaded.Value);
        if (sent.IsSuccess)
            _cursor.Set(loaded.Value.Max(document => document.DocumentNumber));

        return delaySeconds;
    }

    private static int PollSeconds(int value) =>
        value <= 0 ? 60 : Math.Max(15, value);
}
