using Domain.Frontol.Models.DeferredReceipts;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record DeferredReceiptsResponse : Message
{
    public DeferredReceiptsResponse()
    {
        MessageType = MessageType.DeferredReceipts;
    }

    public DeferredReceiptOperation Operation { get; set; }

    public bool Success { get; set; } = true;

    public string Error { get; set; } = string.Empty;

    public List<DeferredReceipt> Receipts { get; set; } = [];

    public List<PaymentKind> PaymentKinds { get; set; } = [];

    public List<PrintGroupInfo> PrintGroups { get; set; } = [];
}
