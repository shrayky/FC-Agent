using System.Runtime.InteropServices;
using Domain.Agent.Dto;
using Domain.Configuration.Constants;

namespace Domain.Agent;

public static class AgentDataFactory
{
    public static AgentData Current(
        IReadOnlyList<string> installedRuntimes,
        string mainGdbPath = "",
        string logGdbPath = "",
        IReadOnlyList<PhysicalDiskHealth>? disks = null)
    {
        return new AgentData
        {
            Version = ApplicationInformation.Version,
            Assembly = ApplicationInformation.Assembly,
            Os = "windows",
            Architecture = AgentArchitecture.Name(RuntimeInformation.ProcessArchitecture),
            HostName = MachineNetworkInfo.HostName(),
            IpAddresses = MachineNetworkInfo.ListAddresses(),
            InstalledRuntimes = [..installedRuntimes],
            Disks = [..(disks ?? [])],
            MainGdbSize = DriveMetricsReader.FileSize(mainGdbPath),
            LogGdbSize = DriveMetricsReader.FileSize(logGdbPath)
        };
    }
}
