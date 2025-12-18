using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;

namespace Domain.Frontol.Interfaces;

public interface IFrontolSettings
{
    Task<Result> LoadGlobalControlConfig(GlobalControl globalControl);
    Task<Result<GlobalControl>> GetGlobalControlConfig();
}