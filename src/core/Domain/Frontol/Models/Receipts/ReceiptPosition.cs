namespace Domain.Frontol.Models.Receipts;

public record ReceiptPosition
{
    public int WareCode { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Mark { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public double Quantity { get; init; }

    public double Price { get; init; }

    public double Summ { get; init; }

    public int WareType { get; init; }

    public int PrintGroupCode { get; init; }

    public bool Storno { get; init; }
}
