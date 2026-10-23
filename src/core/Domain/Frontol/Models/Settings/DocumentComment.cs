namespace Domain.Frontol.Models.Settings;

public record DocumentComment
{
    public int Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
