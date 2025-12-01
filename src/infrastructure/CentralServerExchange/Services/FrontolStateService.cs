using Domain.Agent.Dto;
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
    private readonly AtolLicenseService _atolLicenseService;

    public FrontolStateService(ILogger<FrontolStateService> logger, IParametersService parametersService, IServiceScopeFactory scopeFactory, AtolLicenseService atolLicenseService)
    {
        _logger = logger;
        _parametersService = parametersService;
        _scopeFactory = scopeFactory;
        _atolLicenseService = atolLicenseService;
    }

    public async Task<AgentStateResponse> Current()
    {
        var settings = await _parametersService.Current();

        var frontolVersion = string.Empty;
        
        if (!string.IsNullOrEmpty(settings.DatabaseConnection.DatabasePath))
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFrontolMainDb>();
            
            var version = await repository.Version();
            
            if (version.IsSuccess) 
                frontolVersion = version.Value;
        }
        
        AgentStateResponse state = new()
        {
            AgentToken = settings.CentralServerSettings.Token,
            
            AgentInformation = new AgentData
            {
                Version = ApplicationInformation.Version,
                Assembly = ApplicationInformation.Assembly,    
            },
            
            FrontolVersion = frontolVersion,
            Licenses = _atolLicenseService.FromFiles(),
        };
        
        _logger.LogDebug(state.ToString());
        
        return state;
    }
}