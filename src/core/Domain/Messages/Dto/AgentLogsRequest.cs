using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record AgentLogsRequest : Message
{
    public AgentLogsRequest()
    {
        MessageType = MessageType.AgentLogs;
    }

    /// <summary>
    /// Что отдать агенту: пусто — только список файлов, "now" — самый свежий файл,
    /// иначе суффикс даты из имени файла лога (например 20260926).
    /// </summary>
    public string SelectedLogFileName { get; set; } = string.Empty;
}
