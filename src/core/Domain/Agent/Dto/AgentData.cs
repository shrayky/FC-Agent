namespace Domain.Agent.Dto;

public class AgentData
{
    public int Version { get; set; }
    public int Assembly { get; set; }
    public string Os { get; set; } = "windows";
    public string Architecture { get; set; } = "x86";
    public string HostName { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = [];
    public List<string> InstalledRuntimes { get; set; } = [];
    public long WindowsDiskSize { get; set; }
    public long WindowsDiskFreeSpace { get; set; }
    public string WindowsDiskLetter { get; set; } = string.Empty;
    public string WindowsDiskName { get; set; } = string.Empty;
    public long DatabaseDiskSize { get; set; }
    public long DatabaseDiskFreeSpace { get; set; }
    public string DatabaseDiskLetter { get; set; } = string.Empty;
    public string DatabaseDiskName { get; set; } = string.Empty;
    public long MainGdbSize { get; set; }
    public long LogGdbSize { get; set; }
}
