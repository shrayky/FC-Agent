namespace Domain.Frontol.Dto;

public record FrontolSettings
{
    public GlobalControl GlobalControl { get; set; } = new();
    public bool Updated { get; set; }
}