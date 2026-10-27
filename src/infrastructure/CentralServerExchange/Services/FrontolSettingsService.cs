using CSharpFunctionalExtensions;
using Domain.Configuration.Constants;
using Domain.Frontol;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.FilesFolders;

namespace CentralServerExchange.Services;

public class FrontolSettingsService
{
    private readonly ILogger<FrontolSettingsService> _logger;
    private readonly IServiceScopeFactory _serviceScope;
    private readonly IFrontolTariff _tariff;

    public FrontolSettingsService(
        ILogger<FrontolSettingsService> logger,
        IServiceScopeFactory serviceScope,
        IFrontolTariff tariff)
    {
        _logger = logger;
        _serviceScope = serviceScope;
        _tariff = tariff;
    }

    public async Task<Result<FrontolSettings>> ReadFrontolSettings()
    {
        using var scope = _serviceScope.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFrontolSettings>();
        var userProfilesRepository = scope.ServiceProvider.GetRequiredService<IFrontolUserProfiles>();
        var actionScriptRepository = scope.ServiceProvider.GetRequiredService<IFrontolActionScripts>();
        var cashRegisterScripts = scope.ServiceProvider.GetRequiredService<IFrontolCashRegisterDriverScripts>();
        var scriptLibraries = scope.ServiceProvider.GetRequiredService<IFrontolScriptLibraries>();
        var documents = scope.ServiceProvider.GetRequiredService<IFrontolDocumentConfiguration>();

        var globalConfig = await repository.GetGlobalControlConfig();
        var parameters = await repository.GetParameters();
        var userProfiles = await userProfilesRepository.GetUserProfiles();
        var frontolScript = await actionScriptRepository.FromDb();
        var driverScripts = await cashRegisterScripts.FromFiles();
        var libraries = await scriptLibraries.FromFiles();
        var documentConfiguration = await documents.Read();

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

        if (documentConfiguration.IsFailure)
            return Result.Failure<FrontolSettings>(documentConfiguration.Error);

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
            Scripts = scripts,
            DocumentKinds = documentConfiguration.Value.DocumentKinds,
            DocumentTemplates = documentConfiguration.Value.DocumentTemplates,
            PrintGroups = documentConfiguration.Value.PrintGroups,
            Comments = documentConfiguration.Value.Comments
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
        var documents = scope.ServiceProvider.GetRequiredService<IFrontolDocumentConfiguration>();
        var scripts = await ScriptTargetsFor(settings);

        // Bind, не Tap: Tap отбрасывает Result шага, и наверх уходит успех,
        // хотя запись шаблона или скрипта уже упала.
        // Секции базы — одна транзакция. Файлы после фиксации: каталоги скриптов
        // откатить вместе с Firebird нельзя. Перезапуск — только когда записаны оба.
        var written = mainRepository.BeginWrite()
            .Bind(() => settingsRepository.LoadGlobalControlConfig(settings.GlobalControl))
            .Bind(() => settingsRepository.LoadParameters(settings.Settings ?? []))
            .Bind(() => userRepository.LoadUserProfiles(settings.UserProfiles));

        if (scripts.StartScript)
            written = written.Bind(() => actionScriptRepository.ToDb(WithLibraryPath(settings.Scripts.FrontolScript)));

        var writtenResult = await written
            .Bind(() => documents.Apply(new DocumentConfiguration
            {
                DocumentKinds = settings.DocumentKinds,
                DocumentTemplates = settings.DocumentTemplates,
                PrintGroups = settings.PrintGroups,
                Comments = settings.Comments
            }))
            .Bind(() => mainRepository.CommitWrite());

        if (writtenResult.IsFailure)
        {
            await mainRepository.RollbackWrite();
            return writtenResult;
        }

        if (!scripts.Files)
            return await mainRepository.Restart();

        return await scriptLibraries.ToFiles(settings.Scripts.ScriptLibraries)
            .Bind(() => cashRegisterScripts.ToFiles(settings.Scripts.CashRegisterDriver10Scripts, settings.Scripts.UploadCashRegisterScripts))
            .Bind(() => mainRepository.Restart());
    }

    // Пустой текст не вызывает ToDb: код 99999 и имя main затёрли бы существующий ACTSCRIPT.
    // Действующий тариф «Базовый» пропускает и файлы скриптов. Непроверенный тариф запись не блокирует.
    private async Task<ScriptTargets> ScriptTargetsFor(FrontolSettings settings)
    {
        if (await _tariff.Current() == FrontolTariffState.ActiveBasic)
        {
            _logger.LogInformation("Тариф «Базовый»: скрипты Frontol не записываются");
            return new ScriptTargets(false, false);
        }

        if (string.IsNullOrWhiteSpace(settings.Scripts.FrontolScript.Text))
        {
            _logger.LogInformation("Стартовый скрипт не задан: ACTSCRIPT и StartScript не меняются");
            return new ScriptTargets(false, true);
        }

        return new ScriptTargets(true, true);
    }

    private readonly record struct ScriptTargets(bool StartScript, bool Files);

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