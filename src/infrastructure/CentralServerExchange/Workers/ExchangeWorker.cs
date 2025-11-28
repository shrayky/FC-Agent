using CentralServerExchange.Services;
using Microsoft.Extensions.Hosting;

namespace CentralServerExchange.Workers;

public class ExchangeWorker : BackgroundService
{
    private readonly SignalRAgentClient _signalRClient;
    private readonly FrontolStateService _frontolStateService;
    private readonly FrontolLogsService _frontolLogsService;
    
    public ExchangeWorker(SignalRAgentClient signalRClient, FrontolStateService frontolStateService, FrontolLogsService frontolLogsService)
    {
        _signalRClient = signalRClient;
        _frontolStateService = frontolStateService;
        _frontolLogsService = frontolLogsService;
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

                var logs = await _frontolLogsService.Collect();
                var sendLogsResult = await _signalRClient.SendFrontolLogs(logs);
                if (sendLogsResult.IsSuccess)
                    _frontolLogsService.SetMaxLogId(logs.Max(f => f.Id));

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