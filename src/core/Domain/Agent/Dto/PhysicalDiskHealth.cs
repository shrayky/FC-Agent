namespace Domain.Agent.Dto;

public class PhysicalDiskHealth
{
    public string Letter { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public long Size { get; set; }
    public long FreeSpace { get; set; }
    public bool IsOs { get; set; }
    public bool IsDatabase { get; set; }
    public int? LifePercent { get; set; }
    public long? BadBlocks { get; set; }
    public int? PowerOnDays { get; set; }
}
