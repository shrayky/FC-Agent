using Domain.Agent.Dto;
using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record NewVersionRequest : Message
{
    public NewVersionRequest()
    {
        MessageType = MessageType.NewVersionAsk;
    }

    public AgentData AgentInformation { get; set; } = new();
}
