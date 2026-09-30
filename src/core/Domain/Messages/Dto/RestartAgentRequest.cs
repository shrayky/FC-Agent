using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record RestartAgentRequest : Message
{
    public RestartAgentRequest()
    {
        MessageType = MessageType.RestartAgent;
    }
}
