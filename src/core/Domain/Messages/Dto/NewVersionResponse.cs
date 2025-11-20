using Domain.Messages.Enums;
using Domain.Messages.Interfaces;

namespace Domain.Messages.Dto;

public record NewVersionResponse : IMessage
{
    public string AgentToken { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.NewVersionAsk;
    public bool Need { get; set;}
    public string NewVersion { get; set;} = string.Empty;
    public string UpdateId { get; set;} = string.Empty;
    public string UpdateHash { get; set;} = string.Empty;
}