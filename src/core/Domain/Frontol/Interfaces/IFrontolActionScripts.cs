using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Interfaces;

public interface IFrontolActionScripts
{
    Task<Result> ToDb(ActionScript actionScript);
    Task<Result<ActionScript>> FromDb();
}
