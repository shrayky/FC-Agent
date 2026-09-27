using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record AgentLogsResponse : Message
{
    public AgentLogsResponse()
    {
        MessageType = MessageType.AgentLogs;
    }

    public List<string> LogFilesNames { get; set; } = [];

    public string SelectedLogFileName { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool Success { get; set; } = true;

    public string Error { get; set; } = string.Empty;
}
