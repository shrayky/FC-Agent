using CSharpFunctionalExtensions;

namespace Domain.Frontol.Interfaces;

public interface IFrontolMainDb
{
    Task<Result<string>> Version();
    Task<Result> Restart();
    Task<int> NextChangeId();
}