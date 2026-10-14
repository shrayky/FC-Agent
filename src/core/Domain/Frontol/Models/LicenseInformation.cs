namespace Domain.Frontol.Models;

public record LicenseInformation
{
    public string Id { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public bool IsTariff { get; set; }
    public DateTime ActivatedAt { get; set; }
    public int TariffDaysLeft { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TariffName { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public bool IsPerpetual { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public int ConnectionCount { get; set; }
    public bool IsTrial { get; set; }
}
