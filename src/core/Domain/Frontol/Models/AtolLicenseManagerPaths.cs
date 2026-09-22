namespace Domain.Frontol.Models;

// Консольный менеджер лицензий АТОЛ стоит в Program Files системного диска;
// на 32-битной ОС ProgramFilesX86 пуст, поэтому проверяются оба каталога.
public static class AtolLicenseManagerPaths
{
    private const string RelativePath = @"ATOL\LicManager\LicenseManager_con.exe";

    public static string? Resolve(Func<Environment.SpecialFolder, string> folderPath, Func<string, bool> fileExists)
    {
        var folders = new[]
        {
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.ProgramFiles
        };

        foreach (var folder in folders)
        {
            var root = folderPath(folder);

            if (string.IsNullOrWhiteSpace(root))
                continue;

            var candidate = Path.Combine(root, RelativePath);

            if (fileExists(candidate))
                return candidate;
        }

        return null;
    }
}
