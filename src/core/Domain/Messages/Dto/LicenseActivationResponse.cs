using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record LicenseActivationResponse : Message
{
    public LicenseActivationResponse()
    {
        MessageType = MessageType.LicenseActivation;
    }

    public string LicenseId { get; set; } = string.Empty;

    public bool Success { get; set; } = true;

    public string Error { get; set; } = string.Empty;
}
