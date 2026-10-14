using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Cashiers;

namespace Domain.Frontol.Interfaces;

public interface IFrontolCashiers
{
    Task<Result> Apply(int offset, IReadOnlyList<CashierItem> cashiers);
}
