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
    /// Текущая версия, ОС, архитектура, .NET, имя ПК, IP и размеры дисков/gdb.
    /// </summary>
    public static AgentData Current(
        IReadOnlyList<string> installedRuntimes,
        string mainGdbPath = "",
        string logGdbPath = "")
    {
        var windowsDisk = DriveMetricsReader.WindowsSystemDrive();
        var databaseDisk = DriveMetricsReader.FromPath(mainGdbPath);

        return new AgentData
        {
            Version = ApplicationInformation.Version,
            Assembly = ApplicationInformation.Assembly,
            Os = "windows",
            Architecture = AgentArchitecture.Name(RuntimeInformation.ProcessArchitecture),
            HostName = MachineNetworkInfo.HostName(),
            IpAddresses = MachineNetworkInfo.ListAddresses(),
            InstalledRuntimes = [..installedRuntimes],
            WindowsDiskSize = windowsDisk.TotalBytes,
            WindowsDiskFreeSpace = windowsDisk.FreeBytes,
            WindowsDiskLetter = windowsDisk.Letter,
            WindowsDiskName = windowsDisk.Name,
            DatabaseDiskSize = databaseDisk.TotalBytes,
            DatabaseDiskFreeSpace = databaseDisk.FreeBytes,
            DatabaseDiskLetter = databaseDisk.Letter,
            DatabaseDiskName = databaseDisk.Name,
            MainGdbSize = DriveMetricsReader.FileSize(mainGdbPath),
            LogGdbSize = DriveMetricsReader.FileSize(logGdbPath)
        };
    }
}
