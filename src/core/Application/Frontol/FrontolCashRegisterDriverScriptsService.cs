using System.Runtime.Versioning;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Shared.DI.Attributes;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Transient)]
public class FrontolCashRegisterDriverScriptsService : IFrontolCashRegisterDriverScripts
{
    private const string FrontolServiceName = "srvFrontol";
    private const string JsonScriptsFolderName = "json_scripts";

    private static readonly HashSet<string> ProtectedScriptFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "json_beep.js",
        "json_getLastFiscalDocument.js",
        "json_OFDTest.js",
        "myscripts_utils.js"
    };

    private readonly ILogger<FrontolCashRegisterDriverScriptsService> _logger;

    public FrontolCashRegisterDriverScriptsService(ILogger<FrontolCashRegisterDriverScriptsService> logger)
    {
        _logger = logger;
    }

    public async Task<Result<List<AtolCashRegisterDriver10Srcipt>>> FromFiles()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return Result.Success<List<AtolCashRegisterDriver10Srcipt>>([]);

            var directoryResult = ResolveJsonScriptsDirectory();
            if (directoryResult.IsFailure)
                return Result.Failure<List<AtolCashRegisterDriver10Srcipt>>(directoryResult.Error);

            var directory = directoryResult.Value;
            if (!Directory.Exists(directory))
                return Result.Success<List<AtolCashRegisterDriver10Srcipt>>([]);

            var scripts = new List<AtolCashRegisterDriver10Srcipt>();

            foreach (var filePath in Directory.GetFiles(directory))
            {
                scripts.Add(new AtolCashRegisterDriver10Srcipt
                {
                    FileName = Path.GetFileName(filePath),
                    Script = await File.ReadAllTextAsync(filePath)
                });
            }

            return Result.Success(scripts);
        }
        catch (Exception ex)
        {
            var err = $"Ошибка при чтении скриптов драйвера ККТ: {ex}";
            _logger.LogError(err);
            return Result.Failure<List<AtolCashRegisterDriver10Srcipt>>(err);
        }
    }

    public async Task<Result> ToFiles(IReadOnlyList<AtolCashRegisterDriver10Srcipt> scripts, bool upload)
    {
        if (!upload)
            return Result.Success();

        try
        {
            if (!OperatingSystem.IsWindows())
                return Result.Failure("Загрузка скриптов драйвера ККТ поддерживается только в Windows");

            var directoryResult = ResolveJsonScriptsDirectory();
            if (directoryResult.IsFailure)
                return directoryResult;

            var directory = directoryResult.Value;
            Directory.CreateDirectory(directory);

            foreach (var existingFile in Directory.GetFiles(directory))
            {
                var existingName = Path.GetFileName(existingFile);
                if (ProtectedScriptFileNames.Contains(existingName))
                    continue;

                File.Delete(existingFile);
            }

            foreach (var script in scripts)
            {
                var fileName = Path.GetFileName(script.FileName);
                if (string.IsNullOrWhiteSpace(fileName) || ProtectedScriptFileNames.Contains(fileName))
                    continue;

                var filePath = Path.Combine(directory, fileName);
                await File.WriteAllTextAsync(filePath, script.Script);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            var err = $"Ошибка при сохранении скриптов драйвера ККТ: {ex}";
            _logger.LogError(err);
            return Result.Failure(err);
        }
    }

    [SupportedOSPlatform("windows")]
    private Result<string> ResolveJsonScriptsDirectory()
    {
        var fromService = TryGetFrontolBinFromService();
        if (fromService.IsSuccess)
            return Result.Success(Path.Combine(fromService.Value, JsonScriptsFolderName));

        _logger.LogWarning("{error}. Используется каталог json_scripts по умолчанию", fromService.Error);

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "ATOL",
            "Frontol6",
            "bin",
            JsonScriptsFolderName);

        return Result.Success(fallback);
    }

    [SupportedOSPlatform("windows")]
    private static Result<string> TryGetFrontolBinFromService()
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{FrontolServiceName}");
        if (key is null)
            return Result.Failure<string>($"Служба {FrontolServiceName} не найдена");

        var imagePath = key.GetValue("ImagePath") as string;
        if (string.IsNullOrWhiteSpace(imagePath))
            return Result.Failure<string>($"У службы {FrontolServiceName} не задан ImagePath");

        var exePath = ExtractExecutablePath(Environment.ExpandEnvironmentVariables(imagePath));
        var binDirectory = Path.GetDirectoryName(exePath);

        if (string.IsNullOrWhiteSpace(binDirectory) || !Directory.Exists(binDirectory))
            return Result.Failure<string>($"Каталог bin Frontol не найден по ImagePath службы {FrontolServiceName}: {exePath}");

        return Result.Success(binDirectory);
    }

    private static string ExtractExecutablePath(string imagePath)
    {
        var trimmed = imagePath.Trim();
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
                return trimmed[1..endQuote];
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
            return trimmed[..(exeIndex + 4)];

        return trimmed;
    }
}
