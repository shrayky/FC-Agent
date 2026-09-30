using CSharpFunctionalExtensions;
using Domain.Configuration.Constants;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DI.Attributes;
using Shared.FilesFolders;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Transient)]
public class FrontolScriptLibrariesService : IFrontolScriptLibraries
{
    private readonly ILogger<FrontolScriptLibrariesService> _logger;

    public FrontolScriptLibrariesService(ILogger<FrontolScriptLibrariesService> logger)
    {
        _logger = logger;
    }

    // Каталог данных агента (%ProgramData%\Automation\fc) задаётся в Shared — здесь только подкаталог библиотек.
    public static string DefaultDirectory =>
        FrontolScriptLibraryDirectory.InAgentDataFolder(
            Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name));

    public Task<Result<List<ScriptLibrary>>> FromFiles() =>
        FromFiles(DefaultDirectory);

    public async Task<Result<List<ScriptLibrary>>> FromFiles(string directory)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return Result.Success<List<ScriptLibrary>>([]);

            if (!Directory.Exists(directory))
                return Result.Success<List<ScriptLibrary>>([]);

            var libraries = new List<ScriptLibrary>();

            foreach (var filePath in JavaScriptFiles(directory))
            {
                try
                {
                    libraries.Add(new ScriptLibrary
                    {
                        FileName = Path.GetFileName(filePath),
                        Script = await File.ReadAllTextAsync(filePath)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Не удалось прочитать библиотеку скриптов {file}: {error}", filePath, ex.Message);
                }
            }

            return Result.Success(libraries);
        }
        catch (Exception ex)
        {
            var error = $"Ошибка при чтении библиотек скриптов: {ex}";
            _logger.LogError(error);

            return Result.Failure<List<ScriptLibrary>>(error);
        }
    }

    public Task<Result> ToFiles(IReadOnlyList<ScriptLibrary> libraries) =>
        ToFiles(libraries, DefaultDirectory);

    public async Task<Result> ToFiles(IReadOnlyList<ScriptLibrary> libraries, string directory)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return Result.Failure("Загрузка библиотек скриптов поддерживается только в Windows");

            // Набор библиотек приходит сверху и заменяет файлы каталога. Пустой список — очистить файлы.
            // Корень диска и ProgramData защищены, чтобы опечатка в пути не стёрла чужие файлы.
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            var programData = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)));

            if (string.Equals(normalized, Path.GetPathRoot(normalized), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, programData, StringComparison.OrdinalIgnoreCase))
            {
                var error = $"Отказ: каталог библиотек скриптов {normalized} — корень диска или ProgramData";
                _logger.LogError(error);

                return Result.Failure(error);
            }

            var prepared = Prepare(libraries);
            if (prepared.IsFailure)
            {
                _logger.LogError(prepared.Error);
                return Result.Failure(prepared.Error);
            }

            // Сначала полный набор во временный каталог рядом. fScript меняется только после этого:
            // ошибка имени или записи не должна стирать библиотеки, которые уже лежат на кассе.
            var staging = normalized + ".staging";
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);

            Directory.CreateDirectory(normalized);
            Directory.CreateDirectory(staging);

            foreach (var library in prepared.Value)
            {
                // File.WriteAllTextAsync без Encoding пишет UTF-8 без BOM: BOM перед первой строкой ломает eval() в фронтоле.
                await File.WriteAllTextAsync(Path.Combine(staging, library.FileName), library.Script);
            }

            var deleted = MoveStagedFiles(staging, normalized);
            Directory.Delete(staging);

            _logger.LogInformation(
                "В каталог библиотек скриптов {directory} записано файлов: {written}, удалено лишних: {deleted}",
                normalized,
                prepared.Value.Count,
                deleted);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var error = $"Ошибка при сохранении библиотек скриптов: {ex}";
            _logger.LogError(error);

            return Result.Failure(error);
        }
    }

    // Windows считает устройством и имя с расширением: CON.js открывается как CON.
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static Result<List<ScriptLibrary>> Prepare(IReadOnlyList<ScriptLibrary>? libraries)
    {
        if (libraries is null)
            return Result.Failure<List<ScriptLibrary>>("Список библиотек скриптов не задан");

        var prepared = new List<ScriptLibrary>(libraries.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var library in libraries)
        {
            if (library is null)
                return Result.Failure<List<ScriptLibrary>>("Библиотека скриптов без содержимого");

            var fileName = Path.GetFileName(library.FileName);

            if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
                return Result.Failure<List<ScriptLibrary>>($"Библиотека скриптов без имени файла: {library.FileName}");

            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return Result.Failure<List<ScriptLibrary>>($"Недопустимое имя файла библиотеки скриптов: {fileName}");

            if (IsReservedDeviceName(fileName))
                return Result.Failure<List<ScriptLibrary>>($"Имя файла библиотеки скриптов зарезервировано Windows: {fileName}");

            if (library.Script is null)
                return Result.Failure<List<ScriptLibrary>>($"Не задан текст библиотеки скриптов: {fileName}");

            if (!names.Add(fileName))
                return Result.Failure<List<ScriptLibrary>>($"Повторяющееся имя файла библиотеки скриптов: {fileName}");

            prepared.Add(new ScriptLibrary { FileName = fileName, Script = library.Script });
        }

        return Result.Success(prepared);
    }

    private static bool IsReservedDeviceName(string fileName)
    {
        var stem = fileName;
        var dot = fileName.IndexOf('.');
        if (dot >= 0)
            stem = fileName[..dot];

        return ReservedDeviceNames.Contains(stem.TrimEnd(' ', '.'));
    }

    private static int MoveStagedFiles(string staging, string target)
    {
        var stagedFiles = Directory.GetFiles(staging);
        var stagedNames = new HashSet<string>(
            stagedFiles.Select(file => Path.GetFileName(file)!),
            StringComparer.OrdinalIgnoreCase);

        foreach (var staged in stagedFiles)
            File.Move(staged, Path.Combine(target, Path.GetFileName(staged)!), overwrite: true);

        var deleted = 0;

        foreach (var existing in Directory.GetFiles(target))
        {
            if (stagedNames.Contains(Path.GetFileName(existing)))
                continue;

            File.Delete(existing);
            deleted++;
        }

        return deleted;
    }

    private static IEnumerable<string> JavaScriptFiles(string directory) =>
        Directory.GetFiles(directory)
            .Where(path => Path.GetExtension(path).Equals(".js", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
}
