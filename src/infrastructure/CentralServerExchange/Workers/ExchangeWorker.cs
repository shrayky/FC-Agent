using CentralServerExchange.Services;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Workers;

public class ExchangeWorker : BackgroundService
{
    private readonly ILogger<ExchangeWorker> _logger;
    private readonly SignalRAgentClient _signalRClient;
    private readonly FrontolStateService _frontolStateService;
    
    public ExchangeWorker(ILogger<ExchangeWorker> logger, IParametersService parametersService, SignalRAgentClient signalRClient, FrontolStateService frontolStateService)
    {
        _logger = logger;
        _signalRClient = signalRClient;
        _frontolStateService = frontolStateService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        
        await _signalRClient.StartAsync();  
        
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_signalRClient.ConnectionUp())
                await _signalRClient.StartAsync();

            var agentData = await _frontolStateService.Current();
            
            await _signalRClient.SendAgentDataAsync(agentData);

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}