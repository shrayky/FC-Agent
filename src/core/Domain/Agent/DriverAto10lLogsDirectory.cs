using Microsoft.Win32;

namespace Domain.Agent;

// Логи драйвера ККТ АТОЛ 10 лежат в профиле пользователя, который работает с Frontol.
// Агент работает службой под LocalSystem, а её %USERPROFILE% — это
// C:\Windows\system32\config\systemprofile, поэтому SpecialFolder.UserProfile (и его родитель)
// указывают мимо профилей кассиров: каталоги ищутся по всем профилям машины.
public static class DriverAto10lLogsDirectory
{
    // Служебные каталоги каталога профилей: junction'ы Windows и Public. Имена дублируются
    // по-русски: на локализованной Windows junction «All Users» виден как «Все пользователи».
    private static readonly string[] NonProfileNames =
    [
        "Default", "Default User", "All Users", "Public",
        "Все пользователи", "По умолчанию", "Пользователи по умолчанию"
    ];

    public static readonly string RelativePath =
        Path.Combine("AppData", "Roaming", "ATOL", "Drivers10", "Logs");

    // Каталог профилей машины: реестровый ProfilesDirectory (его меняют, когда профили уводят с системного диска).
    public static string? ProfilesDirectory()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return null;

            return Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList",
                "ProfilesDirectory", null) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Запасной вариант, если реестр недоступен: стандартный каталог профилей системного диска.
    public static string DefaultUsersRoot()
    {
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive");
        return string.IsNullOrWhiteSpace(systemDrive)
            ? Path.Combine("C:", "Users")
            : Path.Combine(systemDrive, "Users");
    }

    public static IReadOnlyList<string> List(
        Func<string?> profilesDirectory,
        Func<string> defaultUsersRoot,
        Func<string, bool> directoryExists)
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in new[] { profilesDirectory(), defaultUsersRoot() })
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
                continue;

            roots.Add(candidate);
        }

        var directories = new List<string>();

        foreach (var root in roots)
        {
            string[] profiles;

            try
            {
                profiles = Directory.GetDirectories(root);
            }
            catch (Exception)
            {
                continue;
            }

            // Порядок фиксирован, чтобы размер по всем профилям считался детерминированно.
            directories.AddRange(
                profiles
                    .Where(profile => !NonProfileNames.Contains(Path.GetFileName(profile), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(profile => Path.GetFileName(profile), StringComparer.Ordinal)
                    .Select(profile => Path.Combine(profile, RelativePath))
                    .Where(directoryExists));
        }

        return directories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
