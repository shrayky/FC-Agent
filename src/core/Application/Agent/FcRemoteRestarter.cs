using CSharpFunctionalExtensions;
using Domain.Agent.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.Agent;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class FcRemoteRestarter : IFcRemoteRestarter
{
    public const string ProcessName = "fc-remote";

    private readonly IFcRemoteProcessSource _source;

    public FcRemoteRestarter(IFcRemoteProcessSource source)
    {
        _source = source;
    }

    public Result Restart()
    {
        try
        {
            foreach (var process in _source.ListByName(ProcessName))
                process.Kill();
        }
        catch (Exception)
        {
            return Result.Success();
        }

        return Result.Success();
    }
}
