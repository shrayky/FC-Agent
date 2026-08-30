using System.Runtime.InteropServices;

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
    /// Читает shared framework из корня dotnet, только если есть host\fxr.
    /// Без hostfxr apphost пишет ".NET location: Not found".
    /// </summary>
    public static List<string> ListFromDotNetRoot(string dotnetRoot)
    {
        if (string.IsNullOrWhiteSpace(dotnetRoot) || !Directory.Exists(dotnetRoot))
            return [];

        var fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        if (!Directory.Exists(fxrDir) || !Directory.EnumerateDirectories(fxrDir).Any())
            return [];

        var shared = Path.Combine(dotnetRoot, "shared");
        return ListFromRoots([shared]);
    }

    /// <summary>
    /// Читает shared framework из dotnet-корня архитектуры текущего процесса.
    /// x86 смотрит Program Files (x86): x64 runtime не закрывает win-x86 apphost.
    /// </summary>
    public static List<string> ListFromWindows()
    {
        var programFiles = RuntimeInformation.ProcessArchitecture == Architecture.X86
            ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (string.IsNullOrEmpty(programFiles))
            return [];

        return ListFromDotNetRoot(Path.Combine(programFiles, "dotnet"));
    }
}
