using Domain.Agent.Dto;
using Domain.Configuration.Constants;
using Domain.DotNet;

namespace Domain.Agent;

/// <summary>
/// Собирает версию агента и сведения о машине для fc.
/// </summary>
public static class AgentDataFactory
{
    public static AgentData Current()
    {
        return new AgentData
        {
            Version = ApplicationInformation.Version,
            Assembly = ApplicationInformation.Assembly,
            HostName = MachineNetworkInfo.HostName(),
            IpAddresses = MachineNetworkInfo.ListAddresses(),
            InstalledRuntimes = InstalledDotNetRuntimes.ListFromWindows()
        };
    }
}
