using Domain.Agent;
using Domain.Configuration.Interfaces;
using Domain.Frontol.Interfaces;
using Domain.Messages.Dto;
using DotNetHost;
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
        var deferredReceiptsCount = 0;
        
        if (!string.IsNullOrEmpty(settings.DatabaseConnection.DatabasePath))
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFrontolMainDb>();
            
            var version = await repository.Version();
            
            if (version.IsSuccess) 
                frontolVersion = version.Value;

            var receipts = scope.ServiceProvider.GetRequiredService<IFrontolDeferredReceipts>();
            var count = await receipts.Count();
            if (count.IsSuccess)
                deferredReceiptsCount = count.Value;
        }
        
        AgentStateResponse state = new()
        {
            AgentToken = settings.CentralServerSettings.Token,
            
            AgentInformation = AgentDataFactory.Current(
                InstalledDotNetRuntimes.ListFromWindows(),
                settings.DatabaseConnection.DatabasePath,
                settings.DatabaseConnection.LogDatabasePath,
                PhysicalDiskHealthReader.List(settings.DatabaseConnection.DatabasePath)),
            
            FrontolVersion = frontolVersion,
            Licenses = _atolLicenseService.FromFiles(),
            DeferredReceiptsCount = deferredReceiptsCount,
        };
        
        _logger.LogDebug(state.ToString());
        
        return state;
    }
}