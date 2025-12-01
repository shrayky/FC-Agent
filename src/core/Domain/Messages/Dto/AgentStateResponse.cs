using Domain.Agent.Dto;
using Domain.Frontol.Dto;
using Domain.Messages.Enums;
using Domain.Messages.Interfaces;

namespace Domain.Messages.Dto;

public record AgentStateResponse : IMessage
{
    public string AgentToken { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.AgentState;
    public AgentData AgentInformation { get; set; } = new();
    public string FrontolVersion { get; set; } = string.Empty;
    public List<LicenseInformation> Licenses { get; set; } = [];
}