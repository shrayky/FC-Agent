using CentralServerExchange.Services;
using Domain.Frontol.Interfaces;
using Microsoft.Extensions.Hosting;

namespace CentralServerExchange.Workers;

public class ExchangeWorker : BackgroundService
{
    private readonly SignalRAgentClient _signalRClient;
    private readonly FrontolStateService _frontolStateService;
    private readonly FrontolLogsService _frontolLogsService;
    private readonly IFrontolMaintenance _maintenance;
    
    public ExchangeWorker(
        SignalRAgentClient signalRClient,
        FrontolStateService frontolStateService,
        FrontolLogsService frontolLogsService,
        IFrontolMaintenance maintenance)
    {
        _signalRClient = signalRClient;
        _frontolStateService = frontolStateService;
        _frontolLogsService = frontolLogsService;
        _maintenance = maintenance;
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

            // Канал не рвём: StopAsync гасит токен клиента, а автопереподключение поднимет его снова.
            // Состояние, логи и запрос версии не шлём — иначе fc получит пустую версию и нулевое число чеков.
            if (_signalRClient.ConnectionUp() && !_maintenance.IsActive)
            {
                var agentData = await _frontolStateService.Current(stoppingToken);
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