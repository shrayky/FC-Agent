using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;

namespace Domain.Frontol.Interfaces;

public interface IFrontolLogDb
{
    Task<Result<LogRecord>> LoadGlobalControlConfig(int fromId, DateTime fromDate);
}