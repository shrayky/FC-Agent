using Domain.Frontol.Models;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record FrontolLogsMessage : Message
{
    public FrontolLogsMessage()
    {
        MessageType = MessageType.FrontolLog;
    }

    public List<LogRecord> Logs { get; set; } = [];
}
