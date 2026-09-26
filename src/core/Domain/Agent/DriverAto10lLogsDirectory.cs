namespace Domain.Agent;

// Логи драйвера ККТ АТОЛ 10 лежат в профиле пользователя, который работает с Frontol.
// Агент работает службой (в том числе под LocalSystem), и SpecialFolder.ApplicationData указывает
// на профиль службы, поэтому каталоги ищутся по всем профилям машины, а не по текущему пользователю.
public static class DriverAto10lLogsDirectory
{
    private static readonly string[] NonProfileNames = ["Default", "Default User", "All Users", "Public"];

    public static readonly string RelativePath =
        Path.Combine("AppData", "Roaming", "ATOL", "Drivers10", "Logs");

    // Корень профилей служба видит одинаково и под LocalSystem, и под пользователем: это C:\Users.
    public static string? UsersRoot()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile) ? null : Path.GetDirectoryName(userProfile);
    }

    public static IReadOnlyList<string> List(Func<string?> usersRoot, Func<string, bool> directoryExists)
    {
        var root = usersRoot();

        if (string.IsNullOrWhiteSpace(root))
            return [];

        string[] profiles;

        try
        {
            profiles = Directory.GetDirectories(root);
        }
        catch (Exception)
        {
            return [];
        }

        // Порядок фиксирован, чтобы размер по всем профилям считался детерминированно.
        return
        [
            .. profiles
                .Where(profile => !NonProfileNames.Contains(Path.GetFileName(profile), StringComparer.OrdinalIgnoreCase))
                .OrderBy(profile => Path.GetFileName(profile), StringComparer.Ordinal)
                .Select(profile => Path.Combine(profile, RelativePath))
                .Where(path => directoryExists(path))
        ];
    }
}
