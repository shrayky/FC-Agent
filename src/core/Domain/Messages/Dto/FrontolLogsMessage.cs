using Domain.Frontol.Dto;
using Domain.Messages.Enums;
using Domain.Messages.Interfaces;

namespace Domain.Messages.Dto;

public record FrontolLogsMessage: IMessage
{
    public string AgentToken { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.FrontolLog;
    public List<LogRecord> Logs { get; set; } = [];

}