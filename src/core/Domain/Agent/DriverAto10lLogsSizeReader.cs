namespace Domain.Agent;

// Логи драйвера АТОЛ: сумма по каталогам всех профилей машины (см. DriverAto10lLogsDirectory).
public static class DriverAto10lLogsSizeReader
{
    public static long TotalBytes()
    {
        try
        {
            var directories = DriverAto10lLogsDirectory.List(
                DriverAto10lLogsDirectory.UsersRoot,
                Directory.Exists);

            return directories.Sum(directory => DirectorySizeBytes(directory));
        }
        catch (Exception)
        {
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

            var options = new EnumerationOptions { IgnoreInaccessible = true };
            var pending = new Stack<string>();
            pending.Push(path);
            long total = 0;

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                total += SizeOfFiles(current, options, onError);

                foreach (var subdirectory in Subdirectories(current, options, onError))
                {
                    // В точки повторной обработки не заходим: обход ушёл бы по кругу, а файлы посчитались дважды.
                    if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0)
                        continue;

                    pending.Push(subdirectory);
                }
            }

            return total;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static long SizeOfFiles(string directory, EnumerationOptions options, Action<string>? onError)
    {
        long total = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", options))
                total += new FileInfo(file).Length;
        }
        catch (Exception ex)
        {
            onError?.Invoke($"{directory}: {ex.Message}");
        }

        return total;
    }

    private static IReadOnlyList<string> Subdirectories(string directory, EnumerationOptions options, Action<string>? onError)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(directory, "*", options)];
        }
        catch (Exception ex)
        {
            onError?.Invoke($"{directory}: {ex.Message}");
            return [];
        }
    }
}
