namespace HostApp.Services;

internal static class ProductProcessPolicy
{
    /// <summary>
    /// В пакете установки обязателен только fc-agent; fc-remote можно не класть.
    /// </summary>
    public static bool IsRequiredInPackage(string productName) =>
        string.Equals(productName, HostConstants.AgentProductName, StringComparison.OrdinalIgnoreCase);
}
