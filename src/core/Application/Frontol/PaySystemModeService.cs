using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Messages.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class PaySystemModeService : IPaySystemModeService
{
    private readonly IFrontolDevices _devices;
    private readonly IPaySystemEmulatorSnapshotStore _snapshot;
    private readonly IFrontolMainDb _mainDb;

    public PaySystemModeService(
        IFrontolDevices devices,
        IPaySystemEmulatorSnapshotStore snapshot,
        IFrontolMainDb mainDb)
    {
        _devices = devices;
        _snapshot = snapshot;
        _mainDb = mainDb;
    }

    public async Task<Result> ChangeMode(PaySystemMode mode)
    {
        try
        {
            return mode == PaySystemMode.Emulator
                ? await SwitchToEmulator()
                : await SwitchToWorkMode();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private async Task<Result> SwitchToEmulator()
    {
        var switched = await _devices.SwitchPaySystemsToEmulator();
        if (switched.IsFailure)
            return Result.Failure(switched.Error);

        if (switched.Value.Count == 0)
            return Result.Failure("Нет банковских систем для перевода в эмулятор");

        var existing = await _snapshot.Load();
        var previousIds = existing.IsSuccess ? existing.Value : [];
        var ids = previousIds.Concat(switched.Value).Distinct().ToList();

        var save = await _snapshot.Save(ids);
        if (save.IsFailure)
            return save;

        return await _mainDb.Restart();
    }

    private async Task<Result> SwitchToWorkMode()
    {
        var snapshot = await _snapshot.Load();
        if (snapshot.IsFailure)
            return Result.Failure(snapshot.Error);

        if (snapshot.Value.Count == 0)
            return Result.Failure("Нет банковских систем для перевода в рабочий режим");

        var switched = await _devices.SwitchPaySystemsToWorkMode(snapshot.Value);
        if (switched.IsFailure)
            return switched;

        var clear = await _snapshot.Clear();
        if (clear.IsFailure)
            return clear;

        return await _mainDb.Restart();
    }
}
