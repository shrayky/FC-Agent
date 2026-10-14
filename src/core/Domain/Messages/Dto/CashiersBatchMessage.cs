using Domain.Frontol.Models.Cashiers;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record CashiersBatchMessage : Message
{
    public CashiersBatchMessage()
    {
        MessageType = MessageType.Cashiers;
    }

    public int Offset { get; set; }
    public string EditId { get; set; } = string.Empty;
    public List<CashierItem> Cashiers { get; set; } = [];
}
