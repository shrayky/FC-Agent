using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DI.Attributes;

namespace Configuration.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class ConfigurationService : IParametersService
    {
        private readonly Lazy<IConfigurationFileManager> _fileManager;
        private readonly Lazy<IConfigurationSerializer> _serializer;
        private readonly Lazy<IConfigurationCacheManager> _cacheManager;
        private readonly Lazy<IConfigurationMigrationService> _migrationService;
        private readonly Lazy<IApplicationState> _applicationState;
        private readonly ILogger<ConfigurationService> _logger;

        public ConfigurationService(IServiceProvider service, ILogger<ConfigurationService> logger) 
        {
            _fileManager = new Lazy<IConfigurationFileManager>(service.GetRequiredService<IConfigurationFileManager>);
            _serializer = new Lazy<IConfigurationSerializer>(service.GetRequiredService<IConfigurationSerializer>);
            _cacheManager = new Lazy<IConfigurationCacheManager>(service.GetRequiredService<IConfigurationCacheManager>);
            _migrationService = new Lazy<IConfigurationMigrationService>(service.GetRequiredService<IConfigurationMigrationService>);
            _applicationState = new Lazy<IApplicationState>(service.GetRequiredService<IApplicationState>);

            _logger = logger;
        }

        public async Task<Parameters> Current()
        {
            var cachedResult = await _cacheManager.Value.GetCachedConfiguration();
            
            if (cachedResult.IsSuccess)
                return cachedResult.Value;

            var configResult = await LoadConfiguration();
            if (configResult.IsSuccess)
            {
                _cacheManager.Value.CacheConfiguration(configResult.Value);
                return configResult.Value;
            }

            _logger.LogError("Не удалось загрузить конфигурацию: {Error}", configResult.Error);
            return new Parameters();
        }

        public async Task<bool> Update(Parameters parameters)
        {
            var validationResult = await _migrationService.Value.ValidateConfiguration(parameters);

            if (validationResult.IsFailure)
            {
                _logger.LogError("Некорректная конфигурация: {Error}", validationResult.Error);
                return false;
            }

            var stored = await LoadConfiguration();

            parameters = await _migrationService.Value.MigrateConfiguration(parameters);

            await _fileManager.Value.SaveConfiguration(parameters);
            await _fileManager.Value.CreateBackup(parameters);

            // Порт, логин и пароль Firebird, уровень файлового лога собираются при старте процесса.
            // Флаг ставим после записи, чтобы выход из процесса был уже с целым config.json.
            RestartWhenStartupBindingChanged(stored.IsSuccess ? stored.Value : null, parameters);

            _cacheManager.Value.CacheConfiguration(parameters);

            _logger.LogInformation("Конфигурация обновлена");
            return true;
        }

        private void RestartWhenStartupBindingChanged(Parameters? previous, Parameters updated)
        {
            // Файл конфигурации не прочитался: сравнивать логин и лог не с чем.
            // Порт 0 и пустые пути — тот же сигнал, что и раньше, когда снимок не удался.
            if (previous is null)
            {
                var connection = updated.DatabaseConnection;
                if (connection.DatabasePath != string.Empty
                    || connection.LogDatabasePath != string.Empty
                    || updated.ServerSettings.ApiIpPort != 0)
                    _applicationState.Value.UpdateNeedRestart(true);

                return;
            }

            if (StartupBindingChanged(previous, updated))
                _applicationState.Value.UpdateNeedRestart(true);
        }

        internal static bool StartupBindingChanged(Parameters previous, Parameters updated)
        {
            var previousConnection = previous.DatabaseConnection;
            var connection = updated.DatabaseConnection;
            var previousLogger = previous.LoggerSettings;
            var logger = updated.LoggerSettings;

            return previousConnection.DatabasePath != connection.DatabasePath
                || previousConnection.LogDatabasePath != connection.LogDatabasePath
                || previousConnection.UserName != connection.UserName
                || previousConnection.Password != connection.Password
                || previous.ServerSettings.ApiIpPort != updated.ServerSettings.ApiIpPort
                || previousLogger.IsEnabled != logger.IsEnabled
                || previousLogger.LogLevel != logger.LogLevel
                || previousLogger.LogDepth != logger.LogDepth;
        }

        private async Task<Result<Parameters>> LoadConfiguration()
        {
            var configResult = await _fileManager.Value.LoadConfiguration();

            if (configResult.IsFailure)
                return configResult;

            return Result.Success(configResult.Value);
        }

        public bool NeedDoMigration(Parameters parameters)
            => _migrationService.Value.IsMigrationRequired(parameters);

    }
}
