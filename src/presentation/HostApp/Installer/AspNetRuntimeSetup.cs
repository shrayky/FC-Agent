using Domain.DotNet;

namespace HostApp.Installer;

/// <summary>
/// Поиск установщиков runtime в пакете и критерий, нужно ли их запускать.
/// </summary>
internal static class AspNetRuntimeSetup
{
    public static readonly string[] RequiredRuntimes =
    [
        "Microsoft.NETCore.App/10.0",
        "Microsoft.AspNetCore.App/10.0"
    ];

    /// <summary>
    /// exe из runtime пакета: сначала .NET Runtime (hostfxr), затем ASP.NET Core.
    /// </summary>
    public static IReadOnlyList<string> FindInstallers(string setupFolder)
    {
        var dir = Path.Combine(setupFolder, "runtime");
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir, "*.exe")
            .OrderBy(InstallerOrder)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Нужно ставить runtime, если нет NETCore 10 или AspNetCore 10.
    /// </summary>
    public static bool NeedsInstall(IReadOnlyList<string> installed) =>
        RequiredRuntimes.Any(required => !DotNetRuntimeRequirement.IsSatisfied(installed, required));

    /// <summary>
    /// 0 — успех, 3010 — успех с запросом перезагрузки (перезагрузку не делаем).
    /// </summary>
    public static bool IsSuccessExitCode(int code) => code is 0 or 3010;

    /// <summary>
    /// Порядок установки: hostfxr+NETCore, затем AspNetCore, остальные exe в конце.
    /// </summary>
    private static int InstallerOrder(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith("dotnet-runtime", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (name.StartsWith("aspnetcore-runtime", StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }
}
