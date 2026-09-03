using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record FrontolSettingsApplyingState : Message
{
    public FrontolSettingsApplyingState()
    {
        MessageType = MessageType.FrontolSettingsApplying;
    }

    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
