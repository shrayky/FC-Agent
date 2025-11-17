namespace Domain.Frontol.Dto;

public class FrontolInformation
{
    public string Version { get; set; } = string.Empty;
    public Frontol.Dto.FrontolSettings Settings { get; set; } = new();
}