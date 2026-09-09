namespace Domain.Messages.Dto;

public record SalesDocument
{
    public long DocumentNumber { get; init; }
    public DateOnly DocumentDate { get; init; }
    public TimeOnly DocumentTime { get; init; }
    public int CheckNumber { get; init; }
    public string CashierCode { get; init; } = string.Empty;
    public string CashierName { get; init; } = string.Empty;
    public double Sum { get; init; }
    public int DocumentType { get; init; }
    public List<SalesPosition> Positions { get; init; } = [];
    public List<SalesPayment> Payments { get; init; } = [];
}
