using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Models;

public class FrontolInformation
{
    public string Version { get; set; } = string.Empty;
    public FrontolSettings Settings { get; set; } = new();
}