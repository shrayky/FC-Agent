using Domain.Frontol.Dto;
using Domain.Messages.Enums;
using Domain.Messages.Interfaces;

namespace Domain.Messages.Dto;

public record FrontolSettingsResponse: IMessage
{
    public string AgentToken { get; set; }  = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.FrontolSettings;
    public FrontolSettings Settings { get; set; } = new FrontolSettings();
}