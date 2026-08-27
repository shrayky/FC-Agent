namespace DotNetHost;

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
        var seenItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!seenRoots.Add(normalizedRoot))
                continue;

            foreach (var frameworkDir in Directory.EnumerateDirectories(normalizedRoot))
            {
                var frameworkName = Path.GetFileName(frameworkDir);
                if (string.IsNullOrEmpty(frameworkName))
                    continue;

                foreach (var versionDir in Directory.EnumerateDirectories(frameworkDir))
                {
                    var versionName = Path.GetFileName(versionDir);
                    if (string.IsNullOrEmpty(versionName) || !Version.TryParse(versionName, out _))
                        continue;

                    var item = $"{frameworkName}/{versionName}";
                    if (seenItems.Add(item))
                        result.Add(item);
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
}
