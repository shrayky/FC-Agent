namespace HostApp.Services;

internal static class VersionFolderAllocator
{
    /// <summary>
    /// Выбирает имя каталога для новой установки: major.minor, если линии ещё нет;
    /// иначе major.minor.{max+1}, чтобы Latest указывал на только что скопированную папку.
    /// </summary>
    public static string Allocate(string productDirectory, Version packagedVersion)
    {
        var major = packagedVersion.Major;
        var minor = packagedVersion.Minor;
        var baseName = $"{major}.{minor}";

        if (!Directory.Exists(productDirectory))
            return baseName;

        Version? max = null;
        foreach (var dir in Directory.EnumerateDirectories(productDirectory))
        {
            var name = Path.GetFileName(dir);
            if (!Version.TryParse(name, out var version))
                continue;

            if (version.Major != major || version.Minor != minor)
                continue;

            if (max is null || version > max)
                max = version;
        }

        if (max is null)
            return baseName;

        var nextBuild = max.Build < 0 ? 1 : max.Build + 1;
        return $"{major}.{minor}.{nextBuild}";
    }
}
