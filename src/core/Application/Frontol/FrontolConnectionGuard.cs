using Domain.Configuration.Options;
using Domain.Frontol;
using Domain.Frontol.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.Frontol;

public readonly record struct FrontolConnectionCheck(
    bool NeedUpdate,
    string MainPath,
    string LogPath,
    string? Error);

[AutoRegisterService(ServiceLifetime.Transient)]
public class FrontolConnectionGuard
{
    private readonly IFrontolIni _frontolIni;

    public FrontolConnectionGuard(IFrontolIni frontolIni)
    {
        _frontolIni = frontolIni;
    }

    public async Task<FrontolConnectionCheck> Check(DatabaseConnection connection)
    {
        var ini = await _frontolIni.FrontolDbPath();
        if (ini.IsFailure)
            return new FrontolConnectionCheck(false, string.Empty, string.Empty, ini.Error);

        var (iniMain, iniLog) = ini.Value;
        var mainPath = FrontolDbPathComparer.WithConfiguredServer(connection.DatabasePath, iniMain);
        var logPath = FrontolDbPathComparer.WithConfiguredServer(connection.LogDatabasePath, iniLog);
        var same =
            string.Equals(connection.DatabasePath, mainPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(connection.LogDatabasePath, logPath, StringComparison.OrdinalIgnoreCase);

        if (same)
            return new FrontolConnectionCheck(false, string.Empty, string.Empty, null);

        return new FrontolConnectionCheck(true, mainPath, logPath, null);
    }
}
