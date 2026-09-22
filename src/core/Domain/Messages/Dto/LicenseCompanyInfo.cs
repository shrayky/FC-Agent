namespace Domain.Messages.Dto;

public record LicenseCompanyInfo
{
    public string OwnershipType { get; set; } = string.Empty;

    public string Company { get; set; } = string.Empty;

    public string Inn { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Contact { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Phone2 { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string SecretCode { get; set; } = string.Empty;

    public string PartnerCode { get; set; } = string.Empty;
}
