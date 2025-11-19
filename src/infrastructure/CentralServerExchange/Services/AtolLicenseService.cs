using Domain.Frontol.Dto;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class AtolLicenseService
{
    private readonly ILogger<AtolLicenseService> _logger;
    
    public AtolLicenseService(ILogger<AtolLicenseService> logger)
    {
        _logger = logger;
    }

    public List<LicenseInformation> FromFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Получение файлов лицензий только для windows");
            return [];
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var licenseFolderPath = Path.Combine(programData, "Atol", "AtolLicSvc", "license");

        if (!Directory.Exists(licenseFolderPath))
        {
            _logger.LogDebug("Не существует каталога с лицензиями {f}!", licenseFolderPath);
            return [];
        }

        var files = Directory.GetFiles(licenseFolderPath);
        
        var licenseInformation = new List<LicenseInformation>();
        
        _logger.LogDebug("В какталоге {f} обнаружено {n} лицензий", licenseFolderPath, files.Length);

        foreach (var licFile in files)
        {
            licenseInformation.Add(new()
            {
                Id = Path.GetFileNameWithoutExtension(licFile),
            });
        }
        
        return licenseInformation;
        
    }
}