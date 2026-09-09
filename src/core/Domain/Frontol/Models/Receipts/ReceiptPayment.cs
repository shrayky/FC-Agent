namespace Domain.Frontol.Models.Receipts;

public record ReceiptPayment
{
    public int PaymentCode { get; init; }

    public string PaymentName { get; init; } = string.Empty;

    public double Summ { get; init; }

    public int PrintGroupCode { get; init; }
}
