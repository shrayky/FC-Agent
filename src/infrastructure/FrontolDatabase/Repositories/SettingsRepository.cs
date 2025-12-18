using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Metadata;
using FrontolDatabase.Mapping;
using FrontolDatabase.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class SettingsRepository: IFrontolSettings
{
    private readonly ILogger<SettingsRepository> _logger;
    private readonly MainDbCtx _ctx;

    public SettingsRepository(ILogger<SettingsRepository> logger, MainDbCtx ctx)
    {
        _logger = logger;
        _ctx = ctx;
    }

    public async Task<Result> LoadGlobalControlConfig(GlobalControl globalControl)
    {
        if (_ctx.Settings == null)
            return Result.Failure("Не удалось открыть Settings");

        var names = SettingsMetadata<GlobalControl>.Properties
            .Select(p => p.Name)
            .Distinct()
            .ToArray();

        var settings = await _ctx.Settings
            .Where(s => names.Contains(s.Name))
            .ToListAsync();

        string Formatter(object val) => val switch
        {
            YesNoWareEnum yesNo => YesNoWareParsers.MapYesNoWareToDb(yesNo),
            NoWareEnum noEnum => YesNoWareParsers.MapNoWareToDb(noEnum),
            _ => val.ToString() ?? string.Empty
        };

        globalControl.ApplyToSettings(settings, Formatter);

        await _ctx.SaveChangesAsync();
        
        return Result.Success();
    }

    public async Task<Result<GlobalControl>> GetGlobalControlConfig()
    {
        if (_ctx.Settings == null)
            return Result.Failure<GlobalControl>("Не удалось открыть Settings");
        
        var names = SettingsMetadata<GlobalControl>.Properties
            .Select(p => p.Name)
            .Distinct()
            .ToArray();

        var settings = await _ctx.Settings
            .AsNoTracking()
            .Where(s => names.Contains(s.Name))
            .ToListAsync();

        object? Parser(string raw, Type type) =>
            type == typeof(YesNoWareEnum) ? YesNoWareParsers.ParseYesNoWareEnum(raw) :
            type == typeof(NoWareEnum) ? YesNoWareParsers.ParseNoWareEnum(raw) :
            null;

        var control = settings.ApplyFromSettings<GlobalControl>(Parser);

        return Result.Success(control);
    }
}