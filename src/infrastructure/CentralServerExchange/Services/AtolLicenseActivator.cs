using System.Diagnostics;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Domain.Messages.Dto;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class AtolLicenseActivator : IAtolLicenseActivator
{
    private const int TimeoutSeconds = 120;
    private const int OutputLimit = 500;

    private readonly ILogger<AtolLicenseActivator> _logger;

    public AtolLicenseActivator(ILogger<AtolLicenseActivator> logger)
    {
        _logger = logger;
    }

    public async Task<Result> Activate(string licenseId, string shopName, LicenseCompanyInfo company)
    {
        if (!OperatingSystem.IsWindows())
            return Result.Failure("Активация лицензий АТОЛ доступна только на Windows");

        var executable = AtolLicenseManagerPaths.Resolve(Environment.GetFolderPath, File.Exists);

        if (executable == null)
            return Result.Failure(@"Не найден консольный менеджер лицензий АТОЛ ATOL\LicManager\LicenseManager_con.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in LicenseManagerCommandLine.Activate(licenseId, shopName, company))
            startInfo.ArgumentList.Add(argument);

        _logger.LogInformation("Активация лицензии {LicenseId}, магазин {Shop}", licenseId, shopName);

        try
        {
            using var process = Process.Start(startInfo);

            if (process == null)
                return Result.Failure("Не удалось запустить менеджер лицензий АТОЛ");

            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(true);
                return Result.Failure($"Менеджер лицензий не ответил за {TimeoutSeconds} секунд");
            }

            var log = string.Join(
                Environment.NewLine,
                new[] { await output, await errors }.Where(text => !string.IsNullOrWhiteSpace(text)));

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Лицензия {LicenseId} активирована", licenseId);
                return Result.Success();
            }

            // Кодировка вывода утилиты неизвестна, поэтому успех определяется кодом возврата,
            // а текст нужен только для сообщения об ошибке.
            _logger.LogWarning(
                "Активация лицензии {LicenseId} завершилась кодом {Code}: {Output}",
                licenseId,
                process.ExitCode,
                log);

            return Result.Failure(Describe(process.ExitCode, log));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка активации лицензии {LicenseId}", licenseId);
            return Result.Failure($"Ошибка запуска менеджера лицензий: {ex.Message}");
        }
    }

    private static string Describe(int exitCode, string output)
    {
        var tail = output.Trim();

        if (string.IsNullOrWhiteSpace(tail))
            return $"Менеджер лицензий вернул код {exitCode}";

        if (tail.Length > OutputLimit)
            tail = tail[^OutputLimit..];

        return $"Менеджер лицензий вернул код {exitCode}: {tail}";
    }
}
