using Domain.Configuration;
using Domain.Configuration.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DI.Attributes;
using Shared.Json;
using System.Text.Json;

namespace Configuration.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class SidecarConnectionImporter
{
    private readonly ILogger<SidecarConnectionImporter> _logger;

    public SidecarConnectionImporter(ILogger<SidecarConnectionImporter> logger)
    {
        _logger = logger;
    }

    public bool TryImport(Parameters settings, string? executableDirectory = null)
    {
        if (!ConnectionIsEmpty(settings))
            return false;

        var directory = executableDirectory ?? AppContext.BaseDirectory;
        var sidecarPath = Path.Combine(directory, "config.json");

        if (!File.Exists(sidecarPath))
            return false;

        try
        {
            var json = File.ReadAllText(sidecarPath);
            var sidecar = JsonSerializer.Deserialize<CentralServerConnection>(json, JsonSerializeOptionsProvider.Default());

            if (sidecar == null ||
                string.IsNullOrWhiteSpace(sidecar.Address) ||
                string.IsNullOrWhiteSpace(sidecar.Token))
            {
                _logger.LogWarning("Файл {Path} не содержит Address и Token", sidecarPath);
                return false;
            }

            settings.CentralServerSettings.Address = sidecar.Address;
            settings.CentralServerSettings.Token = sidecar.Token;

            _logger.LogInformation("Настройки подключения к серверу загружены из {Path}", sidecarPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось прочитать {Path}", sidecarPath);
            return false;
        }
    }

    /// <summary>
    /// Проверяет, что Address и Token в текущих настройках не заданы.
    /// </summary>
    private static bool ConnectionIsEmpty(Parameters settings)
    {
        return string.IsNullOrWhiteSpace(settings.CentralServerSettings.Address)
            && string.IsNullOrWhiteSpace(settings.CentralServerSettings.Token);
    }
}
