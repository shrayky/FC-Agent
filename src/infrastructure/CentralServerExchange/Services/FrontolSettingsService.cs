using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class FrontolSettingsService
{
    private readonly ILogger<FrontolSettingsService> _logger;
    private readonly IServiceScopeFactory  _serviceScope;

    public FrontolSettingsService(ILogger<FrontolSettingsService> logger, IServiceScopeFactory serviceScope)
    {
        _logger = logger;
        _serviceScope = serviceScope;
    }

    public async Task<Result<FrontolSettings>> ReadFrontolSettings()
    {
        using var scope = _serviceScope.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFrontolSettings>();
        var userProfilesRepository = scope.ServiceProvider.GetRequiredService<IFrontolUserProfiles>();
        var actionScriptRepository = scope.ServiceProvider.GetRequiredService<IFrontolActionScripts>();
        var cashRegisterScripts = scope.ServiceProvider.GetRequiredService<IFrontolCashRegisterDriverScripts>();

        var globalConfig = await repository.GetGlobalControlConfig();
        var userProfiles = await userProfilesRepository.GetUserProfiles();
        var frontolScript = await actionScriptRepository.FromDb();
        var driverScripts = await cashRegisterScripts.FromFiles();

        if (globalConfig.IsFailure)
            return Result.Failure<FrontolSettings>(globalConfig.Error);
        
        if  (userProfiles.IsFailure)
            return Result.Failure<FrontolSettings>(userProfiles.Error);

        if (frontolScript.IsFailure)
            return Result.Failure<FrontolSettings>(frontolScript.Error);

        if (driverScripts.IsFailure)
            return Result.Failure<FrontolSettings>(driverScripts.Error);

        FrontolAgentScripts scripts = new()
        {
            FrontolScript = frontolScript.Value,
            CashRegisterDriver10Scripts = driverScripts.Value
        };

        var packet = new FrontolSettings()
        {
            GlobalControl = globalConfig.Value,
            UserProfiles = userProfiles.Value,
            Scripts = scripts
        };
        
        return Result.Success(packet);
    }

    public async Task<Result> ApplySettings(FrontolSettings settings)
    {
        using var scope = _serviceScope.CreateScope();
        var mainRepository = scope.ServiceProvider.GetRequiredService<IFrontolMainDb>();
        var settingsRepository = scope.ServiceProvider.GetRequiredService<IFrontolSettings>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IFrontolUserProfiles>();
        var actionScriptRepository = scope.ServiceProvider.GetRequiredService<IFrontolActionScripts>();
        var cashRegisterScripts = scope.ServiceProvider.GetRequiredService<IFrontolCashRegisterDriverScripts>();

        var updateSettings = await settingsRepository.LoadGlobalControlConfig(settings.GlobalControl)
            .Tap(async () => await userRepository.LoadUserProfiles(settings.UserProfiles))
            .Tap(async () => await actionScriptRepository.ToDb(settings.Scripts.FrontolScript))
            .Tap(async () => await cashRegisterScripts.ToFiles(settings.Scripts.CashRegisterDriver10Scripts, settings.Scripts.UploadCashRegisterScripts))
            .Tap(async () => await mainRepository.Restart());

        if (updateSettings.IsSuccess)
        {

        }

        return updateSettings;
    }
}