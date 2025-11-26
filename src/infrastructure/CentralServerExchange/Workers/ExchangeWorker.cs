using CentralServerExchange.Services;
using Microsoft.Extensions.Hosting;

namespace CentralServerExchange.Workers;

public class ExchangeWorker : BackgroundService
{
    private readonly SignalRAgentClient _signalRClient;
    private readonly FrontolStateService _frontolStateService;
    
    public ExchangeWorker(SignalRAgentClient signalRClient, FrontolStateService frontolStateService)
    {
        _signalRClient = signalRClient;
        _frontolStateService = frontolStateService;
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

            if (_signalRClient.ConnectionUp())
            {
                var agentData = await _frontolStateService.Current();
                await _signalRClient.SendAgentState(agentData);

                await _signalRClient.AskNewVersion();
            }

#if DEBUG
    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
#else
    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);      
#endif
        }
    }
}