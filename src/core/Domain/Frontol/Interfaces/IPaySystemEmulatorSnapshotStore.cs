using CSharpFunctionalExtensions;

namespace Domain.Frontol.Interfaces;

public interface IPaySystemEmulatorSnapshotStore
{
    Task<Result> Save(IReadOnlyList<int> deviceIds);

    Task<Result<List<int>>> Load();

    Task<Result> Clear();
}
