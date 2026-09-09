namespace Domain.Messages.Dto;

public record SalesPayment
{
    public int PaymentCode { get; init; }
    public string PaymentName { get; init; } = string.Empty;
    public double Sum { get; init; }
}
