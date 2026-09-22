using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Wares;

namespace Domain.Frontol.Interfaces;

public interface IFrontolWares
{
    Task<Result> Apply(IReadOnlyList<WareGroup> groups, IReadOnlyList<WareItem> wares);
}
