namespace Domain.Agent.Interfaces;

public interface IFcRemoteProcess
{
    int Id { get; }
    void Kill();
}
