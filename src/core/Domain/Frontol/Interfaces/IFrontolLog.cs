using Domain.Frontol.Dto;

namespace Domain.Frontol.Interfaces;

public interface IFrontolLog
{
    Task<List<LogRecord>> Collect(int fromId);
    Task<List<LogRecord>> Collect(DateTime fromDate);
}