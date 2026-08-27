namespace Domain.DotNet;

/// <summary>
/// Правило: закрывает ли список установленных runtime требование пакета.
/// </summary>
public static class DotNetRuntimeRequirement
{
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
