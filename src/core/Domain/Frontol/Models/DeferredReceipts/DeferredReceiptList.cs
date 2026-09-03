namespace Domain.Frontol.Models.DeferredReceipts;

public record DeferredReceiptList
{
    public List<DeferredReceipt> Receipts { get; init; } = [];

    public List<PaymentKind> PaymentKinds { get; init; } = [];

    public List<PrintGroupInfo> PrintGroups { get; init; } = [];
}
