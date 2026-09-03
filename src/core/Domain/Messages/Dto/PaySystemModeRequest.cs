using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record PaySystemModeRequest : Message
{
    public PaySystemModeRequest()
    {
        MessageType = MessageType.PaySystemMode;
    }

    public PaySystemMode Mode { get; set; }
}
