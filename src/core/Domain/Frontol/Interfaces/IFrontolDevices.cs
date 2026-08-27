using CSharpFunctionalExtensions;

namespace Domain.Frontol.Interfaces;

public interface IFrontolDevices
{
    Task<Result<List<int>>> SwitchPaySystemsToEmulator();

    Task<Result> SwitchPaySystemsToWorkMode(IReadOnlyList<int> deviceIds);
}
