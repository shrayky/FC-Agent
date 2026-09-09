using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record SalesSyncSettingsRequest : Message
{
    public SalesSyncSettingsRequest()
    {
        MessageType = MessageType.SalesSyncSettings;
    }

    public bool LoadSales { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
}
