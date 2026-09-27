namespace Domain.Agent;

// Логи драйвера АТОЛ: сумма по каталогам всех профилей машины (см. DriverAto10lLogsDirectory).
public static class DriverAto10lLogsSizeReader
{
    // Каталоги обходятся без IgnoreInaccessible: иначе отказ доступа молча даёт 0 байт,
    // и в fc это неотличимо от «логов нет».
    private static readonly EnumerationOptions Options = new() { IgnoreInaccessible = false };

    public static long TotalBytes(Action<string>? onError = null)
    {
        try
        {
            var directories = DriverAto10lLogsDirectory.List(
                DriverAto10lLogsDirectory.UsersRoot,
                Directory.Exists);

            // Без сообщений об ошибке отказ доступа к профилю кассира неотличим от «логов нет».
            long total = 0;

            foreach (var directory in directories)
                total += DirectorySizeBytes(directory, onError);

            return total;
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Логи драйвера АТОЛ: обход профилей не выполнен: {ex.Message}");
            return 0;
        }
    }

    public static long DirectorySizeBytes(string path, Action<string>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return 0;

        try
        {
            if (!Directory.Exists(path))
                return 0;

            var pending = new Stack<string>();
            pending.Push(path);
            long total = 0;

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                total += SizeOfFiles(current, onError);

                foreach (var subdirectory in Subdirectories(current, onError))
                    pending.Push(subdirectory);
            }

            return total;
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Каталог логов АТОЛ {path}: {ex.Message}");
            return 0;
        }
    }

    private static long SizeOfFiles(string directory, Action<string>? onError)
    {
        long total = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", Options))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"Файл лога АТОЛ {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Каталог логов АТОЛ {directory}: {ex.Message}");
        }

        return total;
    }

    // Точки повторной обработки отсекаются до чтения: заход по ним увёл бы обход по кругу,
    // а файлы посчитались бы дважды.
    private static IReadOnlyList<string> Subdirectories(string directory, Action<string>? onError)
    {
        try
        {
            return
            [
                .. Directory.EnumerateDirectories(directory, "*", Options)
                    .Where(subdirectory => (File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0)
            ];
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Каталог логов АТОЛ {directory}: {ex.Message}");
            return [];
        }
    }
}
