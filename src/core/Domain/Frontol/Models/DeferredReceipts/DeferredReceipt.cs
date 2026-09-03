namespace Domain.Frontol.Models.DeferredReceipts;

public record DeferredReceipt
{
    public long Id { get; init; }

    public int CheckNumber { get; init; }

    public DateTime OpenDate { get; init; }

    public DateTime OpenTime { get; init; }

    public double Summ { get; init; }

    public double SummWd { get; init; }

    public double PaidSumm { get; init; }

    public double RemainSumm { get; init; }

    public bool HasPrintGroup { get; init; }

    public List<DeferredReceiptPosition> Positions { get; init; } = [];

    public List<DeferredReceiptPayment> Payments { get; init; } = [];
}
