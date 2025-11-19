namespace Domain.Frontol.Dto;

public class FrontolInformation
{
    public string Version { get; set; } = string.Empty;
    public FrontolSettings Settings { get; set; } = new();
}