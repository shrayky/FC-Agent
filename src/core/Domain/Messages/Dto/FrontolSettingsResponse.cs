using Domain.Frontol.Models.Settings;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record FrontolSettingsResponse : Message
{
    public FrontolSettingsResponse()
    {
        MessageType = MessageType.FrontolSettings;
    }

    public string EditId { get; set; } = string.Empty;
    public FrontolSettings Settings { get; set; } = new();
}
