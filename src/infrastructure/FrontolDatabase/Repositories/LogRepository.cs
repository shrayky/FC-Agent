using Domain.Frontol;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class LogRepository : IFrontolLog
{
    private readonly ILogger<LogRepository> _logger;
    private readonly LogDbCtx _dbContext;

    public LogRepository(ILogger<LogRepository> logger, LogDbCtx dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<List<LogRecord>> Collect(int fromId)
    {
        if (_dbContext.Logs == null) {
            _logger.LogError("Не удалось открыть таблицу с логами");
            return [];
        }

        try
        {
            var errors = await _dbContext.Logs.Where(p => p.Id > fromId && (p.Category == "E" || p.Category == "D"))
                .Select(p => new LogRecord 
                {
                    Id = p.Id,
                    Date = p.Date,
                    Message = p.Action
                })
                .ToListAsync();

            return await AttachPreviousMessages(errors);
        }
        catch (Exception  e)
        {
            _logger.LogError("Ошибка получения логов фронтола по id сообщения {err}!", e.Message);
            
            return [];
        }
    }

    public async Task<List<LogRecord>> Collect(DateTime fromDate)
    {
        if (_dbContext.Logs == null) {
            _logger.LogError("Не удалось открыть таблицу с логами");
            return [];
        }
        
        try
        {
            var errors = await _dbContext.Logs.Where(p => p.Date > fromDate && (p.Category == "E" || p.Category == "D"))
                .Select(p => new LogRecord 
                {
                    Id = p.Id,
                    Date = p.Date,
                    Message = p.Action
                })
                .ToListAsync();

            return await AttachPreviousMessages(errors);
        }
        catch (Exception  e)
        {
            _logger.LogError("Ошибка получения логов фронтола по дате {err}!", e.Message);
            
            return [];
        }
    }

    private async Task<List<LogRecord>> AttachPreviousMessages(List<LogRecord> errors)
    {
        if (errors.Count == 0)
            return errors;

        var previousIds = errors.Select(error => error.Id - 1).ToList();
        var previousById = await _dbContext.Logs!
            .Where(log => previousIds.Contains(log.Id))
            .Select(log => new { log.Id, log.Action })
            .ToDictionaryAsync(log => log.Id, log => log.Action);

        foreach (var error in errors)
        {
            previousById.TryGetValue(error.Id - 1, out var previousAction);
            error.Message = FrontolLogMessage.WithPrevious(error.Message, previousAction);
        }

        return errors;
    }
}