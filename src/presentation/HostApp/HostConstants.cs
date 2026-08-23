namespace HostApp;

/// <summary>
/// Константы host-службы FC Agent.
/// </summary>
internal static class HostConstants
{
    public const string Manufacture = "Automation";
    public const string AppName = "fc";

    /// <summary>Имя Windows-службы и корневого exe host.</summary>
    public const string ServiceName = "fc";

    /// <summary>Имя задачи планировщика — то же, что у fc-agent.</summary>
    public const string GuardTaskName = "fc-guard";

    public const string ServiceDisplayName = "DS:FC Agent";
    public const string HostExeName = "fc.exe";

    public const string AgentProductName = "fc-agent";
    public const string RemoteProductName = "fc-remote";

    public static readonly string[] ProductNames = [AgentProductName, RemoteProductName];

    /// <summary>Порт локального UI агента для правила firewall.</summary>
    public const int ApiHttpPort = 2587;

    /// <summary>Сколько последних версий продукта оставлять на диске.</summary>
    public const int VersionsToKeep = 2;

    /// <summary>Несуществующая legacy-служба: RemoveLegacyInstall не трогает fc.</summary>
    public const string LegacyApiServiceName = "fc-legacy-api";

    /// <summary>Несуществующая legacy-служба: RemoveLegacyInstall не трогает fc.</summary>
    public const string LegacyWebServiceName = "fc-legacy-web";

    public const string LegacyApiFolderName = "fc-legacy-api";
    public const string LegacyWebFolderName = "fc-legacy-web";
    public const string LegacyApiProcessName = "fc-legacy-api";
    public const string LegacyWebProcessName = "fc-legacy-web";
}
