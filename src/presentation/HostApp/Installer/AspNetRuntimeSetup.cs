using Domain.DotNet;

namespace HostApp.Installer;

/// <summary>
/// Поиск установщика ASP.NET Core Runtime в пакете и критерий, нужно ли его запускать.
/// </summary>
internal static class AspNetRuntimeSetup
{
    public const string RequiredRuntime = "Microsoft.AspNetCore.App/10.0";

    /// <summary>
    /// Первый exe в каталоге runtime пакета или null.
    /// </summary>
    public static string? FindInstaller(string setupFolder)
    {
        var dir = Path.Combine(setupFolder, "runtime");
        if (!Directory.Exists(dir))
            return null;

        return Directory.EnumerateFiles(dir, "*.exe")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// Нужно ставить runtime, если требование 10.0 не закрыто.
    /// </summary>
    public static bool NeedsInstall(IReadOnlyList<string> installed) =>
        !InstalledDotNetRuntimes.IsSatisfied(installed, RequiredRuntime);

    /// <summary>
    /// 0 — успех, 3010 — успех с запросом перезагрузки (перезагрузку не делаем).
    /// </summary>
    public static bool IsSuccessExitCode(int code) => code is 0 or 3010;
}
