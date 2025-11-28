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

public class MainDbRepository : IFrontolMainDb
{
    private readonly ILogger<MainDbCtx> _logger;
    private readonly MainDbCtx _ctx;

    public MainDbRepository(ILogger<MainDbCtx> logger, MainDbCtx ctx)
    {
        _logger = logger;
        _ctx = ctx;
    }
    
    private async Task<int> NextChangeId()
    {
        var value = await _ctx.Database
            .SqlQueryRaw<int>("SELECT GEN_ID(GCHNG, 1) FROM RDB$DATABASE")
            .SingleAsync();
        
        return value;
    }

    public async Task<Result<string>> Version()
    {
        if (_ctx.CustomDb == null)
            return Result.Failure<string>("Не удалось открыть CustomDb"); 
        
        const string emptyTable = "Нет записей в таблице баз данных CustomDb";

        try
        {
            var db = await _ctx.CustomDb
                .AsNoTracking()
                .ToListAsync();

            if (db.Count != 0)
                return Result.Success(db[0].VersionFrontol);

            _logger.LogInformation(emptyTable);
            return Result.Failure<string>(emptyTable);
        }
        catch (Exception ex)
        {
            var err = $"Ошибка получения версии фронтола: {ex.Message}"; 
            _logger.LogError(err);
            return Result.Failure<string>(err);
        }
    }

    public async Task<Result> Restart()
    {
        if (_ctx.Settings == null)
            return Result.Failure("Не удалось открыть Settings");
        
        const string restartOption = "InformAboutChanges";

        try
        {
            var newVal = await NextChangeId();

            var setting = await _ctx.Settings
                .SingleOrDefaultAsync(s => s.Name == restartOption);

            if (setting is null)
                return Result.Failure($"Настройка {restartOption} не найдена");

            setting.Value = newVal.ToString();

            await _ctx.SaveChangesAsync();
        
            return Result.Success();
        }
        catch (Exception e)
        {
            _logger.LogError("Ошибка записи настройки {restartOption} {ex}", restartOption, e.Message);
            return Result.Failure(e.Message);
        }
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