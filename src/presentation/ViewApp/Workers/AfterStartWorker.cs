using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Frontol.Interfaces;

namespace ViewApp.Workers
{
    public class AfterStartWorker : BackgroundService
    {
        private readonly ILogger<AfterStartWorker> _logger;
        private readonly IApplicationState _applicationState;
        private readonly IFrontolIni _frontolIni;
        private readonly IParametersService _parametersService;

        public AfterStartWorker(ILogger<AfterStartWorker> logger, IApplicationState applicationState, IFrontolIni frontolIni, IParametersService parametersService)
        {
            _logger = logger;
            _applicationState = applicationState;
            _frontolIni = frontolIni;
            _parametersService = parametersService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogWarning("Служба запущена");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                
                CheckRestartApplication();
                _ = await FillFrontolPathSettings();
            }
        }
        
        private void CheckRestartApplication()
        {
            if (!_applicationState.NeedRestart())
                return;
            
            _logger.LogWarning("Будет произведен перезапуск приложения из-за изменения настроек.");
                    
            Environment.Exit(0);
        }

        private async Task<bool> FillFrontolPathSettings()
        {
            var settings = await _parametersService.Current();

            if (settings.DatabaseConnection.DatabasePath != string.Empty)
                return true;

            var dbPathExtraction = await _frontolIni.MainGdbPath();

            if (dbPathExtraction.IsFailure)
            {
                _logger.LogError("Не удалось получить путь к main.gdb: {err}", dbPathExtraction.Error);
                return false;
            }

            settings.DatabaseConnection.DatabasePath = dbPathExtraction.Value;
            await _parametersService.Update(settings);

            return true;
        }
    }
}
