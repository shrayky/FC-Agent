using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;

namespace Domain.Frontol.Interfaces;

public interface IFrontolMainDb
{
    Task<Result<string>> Version();
    Task<Result> Restart();
    Task<int> NextChangeId();
    Task<Result<GlobalControl>>  GetGlobalControlConfig();
    Task<Result> LoadGlobalControlConfig(GlobalControl globalControl);
}