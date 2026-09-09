using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record SalesCursorResponse : Message
{
    public SalesCursorResponse()
    {
        MessageType = MessageType.SalesCursorRequest;
    }

    public long DocumentNumber { get; set; }
}
