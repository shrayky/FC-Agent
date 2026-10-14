using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Cashiers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class CashierApplyService : ICashierApplyService
{
    private readonly IFrontolCashiers _cashiers;
    private readonly IFrontolMainDb _mainDb;

    public CashierApplyService(IFrontolCashiers cashiers, IFrontolMainDb mainDb)
    {
        _cashiers = cashiers;
        _mainDb = mainDb;
    }

    public async Task<Result> Apply(int offset, IReadOnlyList<CashierItem> cashiers)
    {
        var write = await _cashiers.Apply(offset, cashiers);
        if (write.IsFailure)
            return write;

        return await _mainDb.Restart();
    }
}
