namespace Domain.Agent.Dto;

public class PhysicalDiskHealth
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int? LifePercent { get; set; }
    public long? BadBlocks { get; set; }
    public int? PowerOnDays { get; set; }
    public List<DiskPartition> Partitions { get; set; } = [];
}
