using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Interfaces;

public interface IFrontolScriptLibraries
{
    Task<Result<List<ScriptLibrary>>> FromFiles();

    Task<Result> ToFiles(IReadOnlyList<ScriptLibrary> libraries);
}
