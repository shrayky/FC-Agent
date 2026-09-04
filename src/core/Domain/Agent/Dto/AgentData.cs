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
    public List<PhysicalDiskHealth> Disks { get; set; } = [];
    public long MainGdbSize { get; set; }
    public long LogGdbSize { get; set; }
}
