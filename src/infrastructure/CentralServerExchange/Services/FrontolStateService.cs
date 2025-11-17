using Domain.Angent.Dto;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Domain.Frontol.Dto;
using Domain.Frontol.Interfaces;
using Domain.Messages.Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class FrontolStateService
{
    private readonly ILogger<FrontolStateService> _logger;
    private readonly IParametersService _parametersService;
    private readonly IServiceScopeFactory _scopeFactory;

    public FrontolStateService(ILogger<FrontolStateService> logger, IParametersService parametersService, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _parametersService = parametersService;
        _scopeFactory = scopeFactory;
    }

    public async Task<Message> Current()
    {
        var settings = await _parametersService.Current();

        var frontolInfo = new FrontolInformation();
        
        if (!string.IsNullOrEmpty(settings.DatabaseConnection.DatabasePath))
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFrontolMainDb>();
            
            var version = await repository.Version();
            
            if (version.IsSuccess) 
                frontolInfo.Version = version.Value;

            var globalConfig = await repository.GetGlobalControlConfig();
            
            if (globalConfig.IsSuccess)
                frontolInfo.Settings.GlobalControl = globalConfig.Value;
        }
        
        Message state = new()
        {
            AgentToken = settings.CentralServerSettings.Token,
            AgentInformation = new AgentData
            {
                Version = ApplicationInformation.Version,
                Assembly = ApplicationInformation.Assembly,    
            },
            FrontolInformation = frontolInfo
        };
        
        _logger.LogDebug(state.ToString());
        
        return state;
    }
}