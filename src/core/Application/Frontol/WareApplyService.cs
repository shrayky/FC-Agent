using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Wares;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class WareApplyService : IWareApplyService
{
    private readonly IFrontolWares _wares;
    private readonly IFrontolMainDb _mainDb;

    public WareApplyService(IFrontolWares wares, IFrontolMainDb mainDb)
    {
        _wares = wares;
        _mainDb = mainDb;
    }

    public async Task<Result> Apply(IReadOnlyList<WareGroup> groups, IReadOnlyList<WareItem> wares)
    {
        if (groups.Count == 0 && wares.Count == 0)
            return Result.Success();

        var write = await _wares.Apply(groups, wares);
        if (write.IsFailure)
            return write;

        return await _mainDb.Restart();
    }
}
