using Domain.Messages.Enums;

namespace Domain.Messages.Dto;

public record RestartRemoteRequest : Message
{
    public RestartRemoteRequest()
    {
        MessageType = MessageType.RestartRemote;
    }
}
