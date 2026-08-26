namespace Domain.Agent.Dto;

public class AgentData
{
    public int Version { get; set; }
    public int Assembly { get; set; }
    public string HostName { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = [];
    public List<string> InstalledRuntimes { get; set; } = [];
}
