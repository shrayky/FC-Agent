using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record FrontolSettingsApplyingState
{
    public string AgentToken { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.FrontolSettingsApplying;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}