namespace Domain.Frontol.Models.Settings;

public record TouchLayout
{
    public int Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];
}
