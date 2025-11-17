using CSharpFunctionalExtensions;

namespace Domain.Frontol.Interfaces;

public interface IFrontolIni
{
    Task<Result<string>> MainGdbPath();
}