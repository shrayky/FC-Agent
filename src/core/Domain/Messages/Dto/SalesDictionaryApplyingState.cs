using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record SalesDictionaryApplyingState : Message
{
    public SalesDictionaryApplyingState()
    {
        MessageType = MessageType.SalesDictionaryApplying;
    }

    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
