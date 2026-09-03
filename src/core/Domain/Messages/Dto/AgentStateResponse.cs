using Domain.Agent.Dto;
using Domain.Frontol.Models;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record AgentStateResponse : Message
{
    public AgentStateResponse()
    {
        MessageType = MessageType.AgentState;
    }

    public AgentData AgentInformation { get; set; } = new();
    public string FrontolVersion { get; set; } = string.Empty;
    public List<LicenseInformation> Licenses { get; set; } = [];
}
