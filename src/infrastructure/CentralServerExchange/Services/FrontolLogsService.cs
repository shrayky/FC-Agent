using Domain.Frontol.Dto;
using Domain.Frontol.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class FrontolLogsService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory  _serviceScope;
    private readonly ILogger<FrontolLogsService> _logger;

    private const string CacheId = "LastLogId";

    public FrontolLogsService(IMemoryCache memoryCache, IServiceScopeFactory serviceScope, ILogger<FrontolLogsService> logger)
    {
        _memoryCache = memoryCache;
        _serviceScope = serviceScope;
        _logger = logger;
    }

    public async Task<List<LogRecord>> Collect()
    {
        using var scope = _serviceScope.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFrontolLog>();
        
        _memoryCache.TryGetValue(CacheId, out int lastLogId);

        List<LogRecord> answer;

        if (lastLogId == 0)
        {
            _logger.LogDebug("Получение логов frontol за сегодняшний день");
            answer = await repository.Collect(DateTime.Today);
        }
        else
        {
            _logger.LogDebug("Получение логов frontol начиная с {id}", lastLogId);
            answer = await repository.Collect(lastLogId);
        }
        
        _logger.LogDebug("Получение логов frontol {count}", answer.Count);

        return answer;
    }
    
    public void SetMaxLogId(int logId)
    {
        _logger.LogDebug("Установлен MaxLogId в {LogId}", logId);
        _memoryCache.Set(CacheId, logId);
    }
}