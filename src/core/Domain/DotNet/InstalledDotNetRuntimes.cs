namespace Domain.DotNet;

/// <summary>
/// Читает установленные shared framework из каталогов dotnet\shared.
/// </summary>
public static class InstalledDotNetRuntimes
{
    /// <summary>
    /// Собирает строки Имя/версия из переданных каталогов shared.
    /// </summary>
    public static List<string> ListFromRoots(IEnumerable<string> roots)
    {
        var result = new List<string>();

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (var frameworkDir in Directory.EnumerateDirectories(root))
            {
                var frameworkName = Path.GetFileName(frameworkDir);
                if (string.IsNullOrEmpty(frameworkName))
                    continue;

                foreach (var versionDir in Directory.EnumerateDirectories(frameworkDir))
                {
                    var versionName = Path.GetFileName(versionDir);
                    if (string.IsNullOrEmpty(versionName) || !Version.TryParse(versionName, out _))
                        continue;

                    result.Add($"{frameworkName}/{versionName}");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Читает shared framework из Program Files и Program Files (x86).
    /// </summary>
    public static List<string> ListFromWindows()
    {
        var roots = new List<string>();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrEmpty(programFiles))
            roots.Add(Path.Combine(programFiles, "dotnet", "shared"));

        if (!string.IsNullOrEmpty(programFilesX86))
            roots.Add(Path.Combine(programFilesX86, "dotnet", "shared"));

        return ListFromRoots(roots);
    }

    /// <summary>
    /// Проверяет, закрывает ли список установленных runtime требование пакета.
    /// </summary>
    public static bool IsSatisfied(IEnumerable<string>? installed, string? requiredRuntime)
    {
        if (string.IsNullOrWhiteSpace(requiredRuntime))
            return true;

        if (installed is null)
            return false;

        if (!TryParse(requiredRuntime, out var requiredName, out var requiredVersion))
            return false;

        foreach (var item in installed)
        {
            if (!TryParse(item, out var name, out var version))
                continue;

            if (!string.Equals(name, requiredName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (version.Major != requiredVersion.Major)
                continue;

            if (version >= requiredVersion)
                return true;
        }

        return false;
    }

    private static bool TryParse(string value, out string name, out Version version)
    {
        name = string.Empty;
        version = new Version(0, 0);
        var slash = value.LastIndexOf('/');
        if (slash <= 0 || slash >= value.Length - 1)
            return false;

        name = value[..slash];
        var versionText = value[(slash + 1)..];
        if (!Version.TryParse(versionText, out var parsed))
            return false;

        version = parsed;
        return true;
    }
}
