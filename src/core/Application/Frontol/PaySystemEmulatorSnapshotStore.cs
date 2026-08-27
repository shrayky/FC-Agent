using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Configuration.Constants;
using Domain.Frontol.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;
using Shared.FilesFolders;
using Shared.Json;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class PaySystemEmulatorSnapshotStore : IPaySystemEmulatorSnapshotStore
{
    private readonly string _filePath;

    public PaySystemEmulatorSnapshotStore()
        : this(Path.Combine(
            Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name),
            "pay-systems-emulator.json"))
    {
    }

    internal PaySystemEmulatorSnapshotStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Сохраняет идентификаторы устройств, переведённых в эмулятор.
    /// </summary>
    public async Task<Result> Save(IReadOnlyList<int> deviceIds)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(deviceIds.ToList(), JsonSerializeOptionsProvider.Default());
            await File.WriteAllTextAsync(_filePath, json);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Читает сохранённый список устройств. Ошибка, если файла нет.
    /// </summary>
    public async Task<Result<List<int>>> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return Result.Failure<List<int>>("Файл отключенных банковских систем не найден");

            var json = await File.ReadAllTextAsync(_filePath);
            var ids = JsonSerializer.Deserialize<List<int>>(json, JsonSerializeOptionsProvider.Default()) ?? [];
            return Result.Success(ids);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<int>>(ex.Message);
        }
    }

    /// <summary>
    /// Удаляет файл со списком устройств.
    /// </summary>
    public Task<Result> Clear()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }
}
