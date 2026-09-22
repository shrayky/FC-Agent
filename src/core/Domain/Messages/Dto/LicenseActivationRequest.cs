using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record LicenseActivationRequest : Message
{
    public LicenseActivationRequest()
    {
        MessageType = MessageType.LicenseActivation;
    }

    public string LicenseId { get; set; } = string.Empty;

    public string ShopName { get; set; } = string.Empty;

    public LicenseCompanyInfo Company { get; set; } = new();
}
