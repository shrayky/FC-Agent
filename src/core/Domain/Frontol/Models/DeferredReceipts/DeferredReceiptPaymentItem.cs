namespace Domain.Frontol.Models.DeferredReceipts;

public record DeferredReceiptPaymentItem
{
    public int PaymentCode { get; init; }

    public double Summ { get; init; }

    public int PrintGroupCode { get; init; }
}
