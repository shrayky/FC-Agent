using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Cashiers;

namespace Domain.Frontol.Interfaces;

public interface ICashierApplyService
{
    Task<Result> Apply(int offset, IReadOnlyList<CashierItem> cashiers);
}
