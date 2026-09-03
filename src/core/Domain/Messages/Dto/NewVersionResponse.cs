using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record NewVersionResponse : Message
{
    public NewVersionResponse()
    {
        MessageType = MessageType.NewVersionAsk;
    }

    public bool Need { get; set; }
    public string NewVersion { get; set; } = string.Empty;
    public string UpdateId { get; set; } = string.Empty;
    public string UpdateHash { get; set; } = string.Empty;
}
