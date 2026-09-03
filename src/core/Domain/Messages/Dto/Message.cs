using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public abstract record Message
{
    public string AgentToken { get; set; } = string.Empty;

    public MessageType MessageType { get; set; }
}
