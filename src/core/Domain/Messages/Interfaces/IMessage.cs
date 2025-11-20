using Domain.Messages.Enums;

namespace Domain.Messages.Interfaces;

public interface IMessage
{
    public string AgentToken { get; set; }
    public MessageType MessageType { get; set; }
}