namespace Domain.Frontol.Models.Cashiers;

public record CashierItem
{
    public int Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProfileCode { get; set; }
    public string Password { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
}
