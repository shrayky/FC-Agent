using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record FrontolSettingsRequest : Message
{
    public FrontolSettingsRequest()
    {
        MessageType = MessageType.FrontolSettings;
    }
}
