namespace Domain.Frontol.Dto;

public record LicenseInformation
{
    public string Id { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public bool IsTariff { get; set; }
    public DateTime ActivatedAt { get; set; }
    public int TariffDaysLeft { get; set; }
    public DateTime UpdatedAt { get; set; }
}