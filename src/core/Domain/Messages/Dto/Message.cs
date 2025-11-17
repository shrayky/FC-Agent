using Domain.Angent.Dto;
using Domain.Frontol.Dto;

namespace Domain.Messages.Dto;

public record Message
{
    public string AgentToken { get; set; } = string.Empty;
    public AgentData AgentInformation { get; set; } = new();
    public FrontolInformation FrontolInformation { get; set; } = new();
}