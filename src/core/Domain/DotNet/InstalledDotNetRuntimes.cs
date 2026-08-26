namespace Domain.DotNet;

/// <summary>
/// Читает установленные shared framework из каталогов dotnet\shared.
/// </summary>
public static class InstalledDotNetRuntimes
{
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
