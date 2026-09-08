using CSharpFunctionalExtensions;
using Domain.Agent.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DI.Attributes;

namespace Application.Agent;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class FcRemoteRestarter : IFcRemoteRestarter
{
    public const string ProcessName = "fc-remote";

    private readonly IFcRemoteProcessSource _source;
    private readonly ILogger<FcRemoteRestarter> _logger;

    public FcRemoteRestarter(
        IFcRemoteProcessSource source,
        ILogger<FcRemoteRestarter> logger)
    {
        _source = source;
        _logger = logger;
    }

    public Result Restart()
    {
        try
        {
            var processes = _source.ListByName(ProcessName);
            if (processes.Count == 0)
                _logger.LogWarning("Процесс {ProcessName} не найден", ProcessName);

            foreach (var process in processes)
                process.Kill();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось перезапустить процесс {ProcessName}", ProcessName);
        }

        return Result.Success();
    }
}
