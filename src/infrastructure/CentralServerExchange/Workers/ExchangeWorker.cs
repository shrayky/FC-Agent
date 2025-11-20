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
    private readonly AgentUpdateService _agentUpdateService;
    
    public ExchangeWorker(ILogger<ExchangeWorker> logger, IParametersService parametersService, SignalRAgentClient signalRClient, FrontolStateService frontolStateService, AgentUpdateService agentUpdateService)
    {
        _logger = logger;
        _signalRClient = signalRClient;
        _frontolStateService = frontolStateService;
        _agentUpdateService = agentUpdateService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
#if DEBUG
    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
#else
    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
#endif
                
        await _signalRClient.StartAsync();  
        
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_signalRClient.ConnectionUp())
                await _signalRClient.StartAsync();

            var agentData = await _frontolStateService.Current();
            await _signalRClient.SendAgentState(agentData);

            await _agentUpdateService.AskNewVersionDownload(_signalRClient);

#if DEBUG
    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
#else
    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);      
#endif
        }
    }
}