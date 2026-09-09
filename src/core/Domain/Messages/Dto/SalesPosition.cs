namespace Domain.Messages.Dto;

public record SalesPosition
{
    public int WareCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Barcode { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double Price { get; init; }
    public double Sum { get; init; }
    public double PriceWd { get; init; }
    public double SumWd { get; init; }
    public bool Storno { get; init; }
    public string GroupCode { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
}
