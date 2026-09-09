namespace Domain.Frontol.Models.Receipts;

public record ReceiptPaymentItem
{
    public int PaymentCode { get; init; }

    public double Summ { get; init; }

    public int PrintGroupCode { get; init; }
}
