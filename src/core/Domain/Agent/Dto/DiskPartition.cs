namespace Domain.Agent.Dto;

public class DiskPartition
{
    public string Letter { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public long FreeSpace { get; set; }
    public bool IsOs { get; set; }
    public bool IsDatabase { get; set; }
}
