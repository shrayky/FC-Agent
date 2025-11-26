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

            var prepareUpdate = await DownloadSoftware(requestAddress)
                .Bind(async fileStream => await CheckShaHash(fileStream, updateHash))
                .Bind(async fileStream => await SaveToTemp(fileStream));

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

    private async Task<Result<Stream>> DownloadSoftware(string requestAddress)
    {
        var result = await _httpClient.DownloadFileWithResumeAsync(
            requestAddress,
            Path.Combine(Path.GetTempPath(), ApplicationInformation.Name, "update.zip"),
            _logger,
            cancellationToken: CancellationToken.None);

        if (result.IsFailure)
            return Result.Failure<Stream>(result.Error);

        _logger.LogInformation("Файл обновления успешно загружен");
        return Result.Success(result.Value);
    }

    private async Task<Result<Stream>> CheckShaHash(Stream fileStream, string expectedSha256)
    {
        using var downloadedSha256 = SHA256.Create();
        var hashBytes = await downloadedSha256.ComputeHashAsync(fileStream).ConfigureAwait(false);
        var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
    
        fileStream.Position = 0;

        if (string.Equals(actualHash, expectedSha256))
            return Result.Success(fileStream);
        
        var errorMessage = $"Хэш {actualHash} загруженного файла обновления не совпадает с ожидаемым {expectedSha256}";
        _logger.LogError(errorMessage);
        
        await fileStream.DisposeAsync();
        return Result.Failure<Stream>(errorMessage);
    }

    private async Task<Result<string>> SaveToTemp(Stream stream)
    {
        var tmpFolder = Path.Combine(Path.GetTempPath(), ApplicationInformation.Name);
        var filePath = Path.Combine(tmpFolder, "update.zip");

        try
        {
            if (!Directory.Exists(tmpFolder))
                Directory.CreateDirectory(tmpFolder);

            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation("Обновление загружено в: {FilePath}", filePath);

            return Result.Success(filePath);
        }
        catch (Exception e)
        {
            var errMsg = $"Ошибка копирования скачанного файла обновления в {filePath}: {e.Message}";
            _logger.LogError(errMsg);
            return Result.Failure<string>(errMsg);
        }
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

