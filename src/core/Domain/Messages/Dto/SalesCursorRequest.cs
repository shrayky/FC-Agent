using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record SalesCursorRequest : Message
{
    public SalesCursorRequest()
    {
        MessageType = MessageType.SalesCursorRequest;
    }
}
