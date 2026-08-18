using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Interfaces;

public interface IFrontolSettings
{
    Task<Result> LoadGlobalControlConfig(GlobalControl globalControl);
    
    Task<Result<GlobalControl>> GetGlobalControlConfig();

    Task<Result> SetSetting(string name, string value);
    Task<Result<string>> GetSetting(string name);

    Task<Result> LoadParameters(List<FrontolParameter> parameters);
    Task<Result<List<FrontolParameter>>> GetParameters();

}