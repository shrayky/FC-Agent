namespace Domain.Agent.Interfaces;

public interface IFcRemoteProcessSource
{
    IReadOnlyList<IFcRemoteProcess> ListByName(string processName);
}
