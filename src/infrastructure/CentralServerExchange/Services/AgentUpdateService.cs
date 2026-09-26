using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using CSharpFunctionalExtensions;
using Domain.Configuration.Constants;
using Microsoft.Extensions.Logging;
using Shared.Http;

namespace CentralServerExchange.Services;

public class AgentUpdateService
{
    private readonly ILogger<AgentUpdateService> _logger;
    private readonly HttpClient _httpClient;
    
    private static readonly SemaphoreSlim UpdateLock = new(1, 1);

    public AgentUpdateService(ILogger<AgentUpdateService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<Result> DownloadAndInstall(string requestAddress, string updateHash)
    {
        if (!await UpdateLock.WaitAsync(0))
            return Result.Failure("Обновление уже запущено");

        try
        {
            _logger.LogInformation("Доступно обновление ПО в центральном сервере");

            var prepareUpdate = await DownloadSoftware(requestAddress, updateHash)
                .Bind(path => CheckShaHash(path, updateHash));

            if (prepareUpdate.IsFailure)
            {
                _logger.LogError(prepareUpdate.Error);
                return Result.Failure(prepareUpdate.Error);
            }

            var installResult = InstallUpdate(prepareUpdate.Value);

            if (!installResult.IsFailure)
                return Result.Success();
            
            _logger.LogError(installResult.Error);
            return Result.Failure(installResult.Error);
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    /// <summary>
    /// Скачивает архив обновления в temp. Имя файла содержит ожидаемый хэш, поэтому докачка
    /// продолжит только тот же самый архив и никогда не подхватит недокачанную другую версию.
    /// </summary>
    private async Task<Result<string>> DownloadSoftware(string requestAddress, string updateHash)
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), ApplicationInformation.Name);
        Directory.CreateDirectory(directoryPath);

        var archivePath = ArchivePath(directoryPath, updateHash);

        // Распакованные файлы прошлой попытки не нужны, а недокачанный текущий архив — нужен.
        foreach (var file in Directory.EnumerateFiles(directoryPath))
        {
            if (string.Equals(file, archivePath, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("Не удалось удалить {FilePath} перед загрузкой обновления: {Error}", file, e.Message);
            }
        }

        var result = await _httpClient.DownloadFileWithResumeAsync(
            requestAddress,
            archivePath,
            _logger,
            cancellationToken: CancellationToken.None);

        if (result.IsFailure)
            return Result.Failure<string>(result.Error);

        _logger.LogInformation("Файл обновления успешно загружен");
        return Result.Success(result.Value);
    }

    private static string ArchivePath(string directoryPath, string updateHash)
    {
        // Хэш приходит от сервера, поэтому оставляем от него только hex-символы, чтобы он не сломал путь.
        var safeHash = new string(updateHash.Where(char.IsAsciiHexDigit).ToArray());
        return Path.Combine(directoryPath, $"update-{safeHash}.zip");
    }

    private async Task<Result<string>> CheckShaHash(string filePath, string expectedSha256)
    {
        string actualHash;

        await using (var fileStream = File.OpenRead(filePath))
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = await sha256.ComputeHashAsync(fileStream).ConfigureAwait(false);
            actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        if (string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Success(filePath);

        var errorMessage = $"Хэш {actualHash} загруженного файла обновления не совпадает с ожидаемым {expectedSha256}";
        _logger.LogError(errorMessage);

        // Испорченный файл удаляем: иначе следующая попытка докачала бы его с текущей позиции,
        // и хэш никогда бы не совпал.
        try
        {
            File.Delete(filePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Не удалось удалить файл обновления {FilePath}: {Error}", filePath, e.Message);
        }

        return Result.Failure<string>(errorMessage);
    }

    private Result InstallUpdate(string updateFileName)
    {
        return OperatingSystem.IsWindows() ? UpdateWindowsApp(updateFileName) : Result.Failure("Не поддерживаемая ОС");
    }

    private Result UpdateWindowsApp(string updateFileName)
    {
        var installerPath = Path.Combine(Path.GetTempPath(), ApplicationInformation.Name);

        if (!Directory.Exists(installerPath))
            Directory.CreateDirectory(installerPath);

        ZipFile.ExtractToDirectory(updateFileName, installerPath, true);
        File.Delete(updateFileName);

        Process process = new();
        ProcessStartInfo startInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "cmd.exe",
            CreateNoWindow = true,
            Arguments = $"/c {installerPath}\\{ApplicationInformation.Name}.exe --install",
            RedirectStandardOutput = true,
        };

        _logger.LogWarning("Найдено обновление, запускаю установку {arguments}.", startInfo.Arguments);

        process.StartInfo = startInfo;
        process.Start();

        Task.Delay(TimeSpan.FromMinutes(5));

        return Result.Success();
    }
}

