using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record CashiersApplyingState : Message
{
    public CashiersApplyingState()
    {
        MessageType = MessageType.CashiersApplying;
    }

    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string EditId { get; set; } = string.Empty;
}
