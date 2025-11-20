using Domain.Agent.Dto;
using Domain.Messages.Enums;
using Domain.Messages.Interfaces;

namespace Domain.Messages.Dto;

public record NewVersionRequest : IMessage
{
    public string AgentToken { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.NewVersionAsk;
    public AgentData AgentInformation { get; set; } = new();
}