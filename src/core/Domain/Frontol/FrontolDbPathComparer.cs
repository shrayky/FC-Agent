namespace Domain.Frontol;

public static class FrontolDbPathComparer
{
    public static bool Same(string configured, string iniPath)
    {
        var left = Normalize(LocalWindowsPath(configured));
        var right = Normalize(LocalWindowsPath(iniPath));

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static string WithConfiguredServer(string configured, string iniPath)
    {
        var local = LocalWindowsPath(iniPath);
        if (string.IsNullOrEmpty(local))
            return iniPath;

        var server = FirstServer(configured) ?? FirstServer(iniPath);
        return server is null ? local : $"{server}:{local}";
    }

    private static string? FirstServer(string path)
    {
        var local = LocalWindowsPath(path);
        if (string.IsNullOrEmpty(local) || path.Length <= local.Length)
            return null;

        var prefix = path[..^local.Length].TrimEnd(':');
        if (string.IsNullOrEmpty(prefix))
            return null;

        return prefix.Split(':')[0];
    }

    // Firebird: [server:][server:]D:\folder\file.gdb — берём от буквы диска.
    private static string LocalWindowsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        for (var i = 0; i < path.Length - 1; i++)
        {
            if (!char.IsLetter(path[i]) || path[i + 1] != ':')
                continue;

            var afterDrive = i + 2;
            var isRoot = afterDrive >= path.Length ||
                         path[afterDrive] is '\\' or '/';
            if (!isRoot)
                continue;

            if (i == 0 || path[i - 1] == ':')
                return path[i..];
        }

        return path;
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }
}
