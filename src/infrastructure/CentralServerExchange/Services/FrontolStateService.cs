using Domain.Angent.Dto;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Domain.Frontol.Dto;
using Domain.Frontol.Interfaces;
using Domain.Messages.Dto;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class FrontolStateService
{
    private readonly ILogger<FrontolStateService> _logger;
    private readonly IParametersService _parametersService;
    private readonly IFrontolMainDb _repository;

    public FrontolStateService(ILogger<FrontolStateService> logger, IParametersService parametersService, IFrontolMainDb repository)
    {
        _logger = logger;
        _parametersService = parametersService;
        _repository = repository;
    }

    public async Task<Message> Current()
    {
        var settings = await _parametersService.Current();

        var frontolInfo = new FrontolInformation();
        
        if (!string.IsNullOrEmpty(settings.DatabaseConnection.DatabasePath))
        {
            var version = await _repository.Version();
            
            if (version.IsSuccess) 
                frontolInfo.Version = version.Value;

            var globalConfig = await _repository.GetGlobalControlConfig();
            
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