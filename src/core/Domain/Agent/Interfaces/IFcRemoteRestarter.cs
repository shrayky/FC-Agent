using CSharpFunctionalExtensions;

namespace Domain.Agent.Interfaces;

public interface IFcRemoteRestarter
{
    Result Restart();
}
