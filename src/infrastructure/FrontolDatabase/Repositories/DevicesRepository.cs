using CSharpFunctionalExtensions;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class DevicesRepository : IFrontolDevices
{
    private readonly ILogger<DevicesRepository> _logger;
    private readonly MainDbCtx _ctx;

    public DevicesRepository(ILogger<DevicesRepository> logger, MainDbCtx ctx)
    {
        _logger = logger;
        _ctx = ctx;
    }

    public async Task<Result<List<int>>> SwitchPaySystemsToEmulator()
    {
        if (_ctx.Devices == null)
            return Result.Failure<List<int>>("Не удалось открыть Devices");

        try
        {
            var devices = await _ctx.Devices
                .Where(d =>
                    d.DeviceType == DeviceTypeEnum.PaySystem &&
                    !d.IsFolder &&
                    d.ConnectionState != DeviceConnectionStateEnum.Emulator)
                .ToListAsync();

            foreach (var device in devices)
                device.ConnectionState = DeviceConnectionStateEnum.Emulator;

            await _ctx.SaveChangesAsync();

            return Result.Success(devices.Select(d => d.Id).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка перевода банковских систем в эмулятор");
            return Result.Failure<List<int>>(ex.Message);
        }
    }

    public async Task<Result> SwitchPaySystemsToWorkMode(IReadOnlyList<int> deviceIds)
    {
        if (_ctx.Devices == null)
            return Result.Failure("Не удалось открыть Devices");

        try
        {
            var devices = await _ctx.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .ToListAsync();

            foreach (var device in devices)
                device.ConnectionState = DeviceConnectionStateEnum.Connected;

            await _ctx.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка перевода банковских систем в рабочий режим");
            return Result.Failure(ex.Message);
        }
    }
}
