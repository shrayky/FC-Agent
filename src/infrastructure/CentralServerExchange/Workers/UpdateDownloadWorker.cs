using CentralServerExchange.Services;
using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Workers;

public class UpdateDownloadWorker : BackgroundService
{
    private readonly ILogger<UpdateDownloadWorker> _logger;
    private readonly IApplicationState _applicationState;
    private readonly IParametersService _parametersService;
    private readonly AgentUpdateService _agentUpdateService;

    public UpdateDownloadWorker(ILogger<UpdateDownloadWorker> logger, IApplicationState applicationState, IParametersService parametersService, AgentUpdateService agentUpdateService)
    {
        _logger = logger;
        _applicationState = applicationState;
        _parametersService = parametersService;
        _agentUpdateService = agentUpdateService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
#else
    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
#endif
        while (!stoppingToken.IsCancellationRequested)
        {
            var updateInfo = _applicationState.NewVersionInformation();

            if (updateInfo.Need)
            {
                _logger.LogWarning("Начинаю скачивать новую версию {version}", updateInfo.NewVersion);
                
                var settings = await _parametersService.Current();
                
                var address = $"{settings.CentralServerSettings.Address}/api/agentFiles/{updateInfo.UpdateId}/download";
                
                await _agentUpdateService.DownloadAndInstall(address, updateInfo.UpdateHash);
            }
            
#if DEBUG
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
#else
    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);      
#endif
        }
        
        
    }
}