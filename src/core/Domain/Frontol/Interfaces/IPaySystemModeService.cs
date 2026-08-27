using CSharpFunctionalExtensions;
using Domain.Messages.Enums;

namespace Domain.Frontol.Interfaces;

public interface IPaySystemModeService
{
    Task<Result> ChangeMode(PaySystemMode mode);
}
