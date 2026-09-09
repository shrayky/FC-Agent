using Application.Frontol;
using Configuration.Services;
using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;

namespace ViewApp.Workers
{
    public class AfterStartWorker : BackgroundService
    {
        private readonly ILogger<AfterStartWorker> _logger;
        private readonly IApplicationState _applicationState;
        private readonly FrontolConnectionGuard _connectionGuard;
        private readonly IParametersService _parametersService;
        private readonly SidecarConnectionImporter _sidecarImporter;

        public AfterStartWorker(
            ILogger<AfterStartWorker> logger,
            IApplicationState applicationState,
            FrontolConnectionGuard connectionGuard,
            IParametersService parametersService,
            SidecarConnectionImporter sidecarImporter)
        {
            _logger = logger;
            _applicationState = applicationState;
            _connectionGuard = connectionGuard;
            _parametersService = parametersService;
            _sidecarImporter = sidecarImporter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var settings = await _parametersService.Current();
            var imported = _sidecarImporter.TryImport(settings);

            if (imported || _parametersService.NeedDoMigration(settings))
            {
                await _parametersService.Update(settings);
            }

            await SyncFrontolDatabasePaths();
            CheckRestartApplication();

            _logger.LogWarning("Служба запущена");

            while (!stoppingToken.IsCancellationRequested)
            {
                CheckRestartApplication();
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        private void CheckRestartApplication()
        {
            if (!_applicationState.NeedRestart())
                return;
            
            _logger.LogWarning("Будет произведен перезапуск приложения из-за изменения настроек.");
                    
            Environment.Exit(0);
        }

        // Пустые пути или расхождение с frontol.ini: ini — какая база у Frontol.
        private async Task SyncFrontolDatabasePaths()
        {
            var settings = await _parametersService.Current();
            var check = await _connectionGuard.Check(settings.DatabaseConnection);

            if (check.Error is not null)
            {
                _logger.LogError("Не удалось проверить путь базы по frontol.ini: {err}", check.Error);
                return;
            }

            if (!check.NeedUpdate)
                return;

            _logger.LogWarning(
                "Путь базы Frontol не совпадает с frontol.ini. Было {oldMain} / {oldLog}, станет {newMain} / {newLog}",
                settings.DatabaseConnection.DatabasePath,
                settings.DatabaseConnection.LogDatabasePath,
                check.MainPath,
                check.LogPath);

            settings.DatabaseConnection.DatabasePath = check.MainPath;
            settings.DatabaseConnection.LogDatabasePath = check.LogPath;

            if (!await _parametersService.Update(settings))
                return;

            if (!_applicationState.NeedRestart())
                _applicationState.UpdateNeedRestart(true);
        }
    }
}
