using Domain.Frontol.Enums;

namespace Domain.Frontol.Models.DeferredReceipts;

public record PaymentKind
{
    public int Code { get; init; }

    public string Name { get; init; } = string.Empty;

    public PaymentOperationEnum Operation { get; init; }

    public int PrintGroupId { get; init; }

    public int PrintGroupCode { get; init; }
}
