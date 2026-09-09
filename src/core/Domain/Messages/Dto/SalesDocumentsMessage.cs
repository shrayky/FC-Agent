using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record SalesDocumentsMessage : Message
{
    public SalesDocumentsMessage()
    {
        MessageType = MessageType.SalesDocuments;
    }

    public List<SalesDocument> Documents { get; set; } = [];
}
