using Domain.Frontol.Models.DeferredReceipts;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record DeferredReceiptsRequest : Message
{
    public DeferredReceiptsRequest()
    {
        MessageType = MessageType.DeferredReceipts;
    }

    public DeferredReceiptOperation Operation { get; set; }

    public long DocumentId { get; set; }

    public List<DeferredReceiptPaymentItem> Payments { get; set; } = [];
}
