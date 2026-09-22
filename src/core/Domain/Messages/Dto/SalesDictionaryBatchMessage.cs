using Domain.Frontol.Models.Wares;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record SalesDictionaryBatchMessage : Message
{
    public SalesDictionaryBatchMessage()
    {
        MessageType = MessageType.SalesDictionary;
    }

    public List<WareGroup> Groups { get; set; } = [];
    public List<WareItem> Wares { get; set; } = [];
}
