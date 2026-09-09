namespace Domain.Frontol.Models.Receipts;

public record ReceiptList
{
    public List<Receipt> Receipts { get; init; } = [];

    public List<PaymentKind> PaymentKinds { get; init; } = [];

    public List<PrintGroupInfo> PrintGroups { get; init; } = [];
}
