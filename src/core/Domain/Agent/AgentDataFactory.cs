using System.Runtime.InteropServices;
using Domain.Agent.Dto;
using Domain.Configuration.Constants;

namespace Domain.Agent;

/// <summary>
/// Собирает версию агента и сведения о машине для fc.
/// </summary>
public static class AgentDataFactory
{
    /// <summary>
    /// Текущая версия, ОС, архитектура, .NET, имя ПК и IP.
    /// </summary>
    public static AgentData Current(IReadOnlyList<string> installedRuntimes)
    {
        return new AgentData
        {
            Version = ApplicationInformation.Version,
            Assembly = ApplicationInformation.Assembly,
            Os = "windows",
            Architecture = AgentArchitecture.Name(RuntimeInformation.ProcessArchitecture),
            HostName = MachineNetworkInfo.HostName(),
            IpAddresses = MachineNetworkInfo.ListAddresses(),
            InstalledRuntimes = [..installedRuntimes]
        };
    }
}
