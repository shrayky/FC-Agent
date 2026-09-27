using CSharpFunctionalExtensions;
using Domain.Entitys.Logs.Interfaces;
using Domain.Messages.Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class AgentLogsService
{
    private readonly ILogger<AgentLogsService> _logger;
    private readonly IServiceScopeFactory _serviceScope;

    public AgentLogsService(ILogger<AgentLogsService> logger, IServiceScopeFactory serviceScope)
    {
        _logger = logger;
        _serviceScope = serviceScope;
    }

    public async Task<Result<AgentLogsResponse>> Collect(string selectedLogFileName)
    {
        using var scope = _serviceScope.CreateScope();
        var collector = scope.ServiceProvider.GetRequiredService<ILogCollectorService>();

        var packet = await collector.Collect(selectedLogFileName);
        _logger.LogDebug(
            "Пакет логов агента: файлов {count}, выбран {selected}",
            packet.LogFileNames.Count,
            packet.SelectedLogFileName);

        // Пустой список — это и выключенное файловое логирование, и пустая папка:
        // серверу нужна причина, а не пустое окно логов.
        if (packet.LogFileNames.Count == 0)
            return Result.Failure<AgentLogsResponse>("Файлы логов агента не найдены");

        if (selectedLogFileName != string.Empty &&
            selectedLogFileName != "now" &&
            !packet.LogFileNames.Contains(selectedLogFileName))
        {
            return Result.Failure<AgentLogsResponse>($"Лог {selectedLogFileName} не найден");
        }

        return Result.Success(new AgentLogsResponse
        {
            LogFilesNames = packet.LogFileNames,
            SelectedLogFileName = packet.SelectedLogFileName,
            Text = packet.LogText
        });
    }
}
