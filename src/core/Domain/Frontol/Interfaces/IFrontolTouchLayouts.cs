using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Interfaces;

public interface IFrontolTouchLayouts
{
    Task<Result<UserInterface>> Read();

    Task<Result> Apply(IReadOnlyList<TouchLayout>? layouts);
}
