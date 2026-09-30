using CSharpFunctionalExtensions;
using Domain.Configuration.Constants;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.FilesFolders;

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
        var scriptLibraries = scope.ServiceProvider.GetRequiredService<IFrontolScriptLibraries>();

        var globalConfig = await repository.GetGlobalControlConfig();
        var parameters = await repository.GetParameters();
        var userProfiles = await userProfilesRepository.GetUserProfiles();
        var frontolScript = await actionScriptRepository.FromDb();
        var driverScripts = await cashRegisterScripts.FromFiles();
        var libraries = await scriptLibraries.FromFiles();

        if (globalConfig.IsFailure)
            return Result.Failure<FrontolSettings>(globalConfig.Error);

        if (parameters.IsFailure)
            return Result.Failure<FrontolSettings>(parameters.Error);
        
        if  (userProfiles.IsFailure)
            return Result.Failure<FrontolSettings>(userProfiles.Error);

        if (frontolScript.IsFailure)
            return Result.Failure<FrontolSettings>(frontolScript.Error);

        if (driverScripts.IsFailure)
            return Result.Failure<FrontolSettings>(driverScripts.Error);

        if (libraries.IsFailure)
            return Result.Failure<FrontolSettings>(libraries.Error);

        FrontolAgentScripts scripts = new()
        {
            FrontolScript = frontolScript.Value,
            CashRegisterDriver10Scripts = driverScripts.Value,
            ScriptLibraries = libraries.Value
        };

        var packet = new FrontolSettings()
        {
            GlobalControl = globalConfig.Value,
            UserProfiles = userProfiles.Value,
            Settings = parameters.Value,
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
        var scriptLibraries = scope.ServiceProvider.GetRequiredService<IFrontolScriptLibraries>();

        var updateSettings = await settingsRepository.LoadGlobalControlConfig(settings.GlobalControl)
            .Tap(async () => await settingsRepository.LoadParameters(settings.Settings ?? []))
            .Tap(async () => await userRepository.LoadUserProfiles(settings.UserProfiles))
            .Tap(async () => await scriptLibraries.ToFiles(settings.Scripts.ScriptLibraries))
            .Tap(async () => await actionScriptRepository.ToDb(WithLibraryPath(settings.Scripts.FrontolScript)))
            .Tap(async () => await cashRegisterScripts.ToFiles(settings.Scripts.CashRegisterDriver10Scripts, settings.Scripts.UploadCashRegisterScripts))
            .Tap(async () => await mainRepository.Restart());

        if (updateSettings.IsSuccess)
        {

        }

        return updateSettings;
    }

    private ActionScript WithLibraryPath(ActionScript script)
    {
        var lineIndex = FrontolScriptHeader.LibraryPathLineIndex(script.Text);

        if (lineIndex >= FrontolScriptLibraryDirectory.HeaderLines)
            _logger.LogWarning("Объявление libPath стоит в строке {line} — ниже заголовка скрипта: "
                + "библиотеки подключаются в начале, скрипт может не найти их", lineIndex + 1);

        return script with
        {
            Text = FrontolScriptHeader.WithLibraryPath(script.Text, ScriptLibraryDirectory)
        };
    }

    // Каталог данных агента (%ProgramData%\Automation\fc) задаётся в Shared — здесь только подкаталог библиотек.
    private static string ScriptLibraryDirectory =>
        FrontolScriptLibraryDirectory.InAgentDataFolder(
            Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name));
}