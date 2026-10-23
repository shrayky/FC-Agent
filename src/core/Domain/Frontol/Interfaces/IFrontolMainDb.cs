using CSharpFunctionalExtensions;

namespace Domain.Frontol.Interfaces;

public interface IFrontolMainDb
{
    Task<Result<string>> Version();
    Task<Result> Restart();
    Task<int> NextChangeId();

    // Одна транзакция на пакет настроек: SaveChanges шагов фиксируются только CommitWrite.
    Task<Result> BeginWrite();
    Task<Result> CommitWrite();
    Task RollbackWrite();
}