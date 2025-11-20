using CSharpFunctionalExtensions;

namespace Domain.Frontol.Interfaces;

public interface IFrontolIni
{
    Task<Result<string>> MainGdbPath();
    Task<Result<(string mainPath, string logPath)>> FrontolDbPath();
}