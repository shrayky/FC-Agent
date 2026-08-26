using System.Runtime.InteropServices;
using Domain.Agent.Dto;
using Domain.Configuration.Constants;
using Domain.DotNet;

namespace Domain.Agent;

/// <summary>
/// Собирает версию агента и сведения о машине для командира.
/// </summary>
public static class AgentDataFactory
{
    /// <summary>
    /// Текущая версия, ОС, архитектура, .NET, имя ПК и IP.
    /// </summary>
    public static AgentData Current()
    {
        return new AgentData
        {
            Version = ApplicationInformation.Version,
            Assembly = ApplicationInformation.Assembly,
            Os = "windows",
            Architecture = RuntimeInformation.OSArchitecture == Architecture.X86 ? "x86" : "x64",
            HostName = MachineNetworkInfo.HostName(),
            IpAddresses = MachineNetworkInfo.ListAddresses(),
            InstalledRuntimes = InstalledDotNetRuntimes.ListFromWindows()
        };
    }
}
