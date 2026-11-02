using Domain.Agent;
using Domain.AppState.Interfaces;
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
    private readonly IAtolLicenseSource _atolLicenseService;
    private readonly IFrontolMaintenance _maintenance;
    private readonly IApplicationState _applicationState;
    private string? _knownFrontolVersion;

    public FrontolStateService(
        ILogger<FrontolStateService> logger,
        IParametersService parametersService,
        IServiceScopeFactory scopeFactory,
        IAtolLicenseSource atolLicenseService,
        IFrontolMaintenance maintenance,
        IApplicationState applicationState)
    {
        _logger = logger;
        _parametersService = parametersService;
        _scopeFactory = scopeFactory;
        _atolLicenseService = atolLicenseService;
        _maintenance = maintenance;
        _applicationState = applicationState;
    }

    public async Task<AgentStateResponse> Current(CancellationToken cancellationToken = default)
    {
        var settings = await _parametersService.Current();

        var frontolVersion = string.Empty;
        var deferredReceiptsCount = 0;
        
        if (!string.IsNullOrEmpty(settings.DatabaseConnection.DatabasePath) && !_maintenance.IsActive)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFrontolMainDb>();
            
            var version = await repository.Version();

            if (version.IsSuccess && !string.IsNullOrEmpty(version.Value))
            {
                NoteFrontolVersion(version.Value);
                frontolVersion = version.Value;
            }

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
                PhysicalDiskHealthReader.List(settings.DatabaseConnection.DatabasePath),
                DriverAto10lLogsSizeReader.TotalBytes(message => _logger.LogWarning(message))),
            
            FrontolVersion = frontolVersion,
            Licenses = (await _atolLicenseService.Read(cancellationToken)).Licenses.ToList(),
            DeferredReceiptsCount = deferredReceiptsCount,
        };
        
        _logger.LogDebug(state.ToString());
        
        return state;
    }

    // Модель базы кешируется на процесс. Другая версия Frontol — другой набор таблиц, нужен новый процесс.
    private void NoteFrontolVersion(string version)
    {
        if (_knownFrontolVersion is null)
        {
            _knownFrontolVersion = version;
            return;
        }

        if (string.Equals(_knownFrontolVersion, version, StringComparison.Ordinal))
            return;

        _logger.LogWarning(
            "Версия Frontol изменилась с {Previous} на {Current}, агент будет перезапущен",
            _knownFrontolVersion,
            version);
        _knownFrontolVersion = version;
        _applicationState.UpdateNeedRestart(true);
    }
}