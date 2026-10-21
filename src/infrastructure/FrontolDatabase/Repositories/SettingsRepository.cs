using CSharpFunctionalExtensions;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using FrontolDatabase.Mapping;
using FrontolDatabase.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class SettingsRepository: IFrontolSettings
{
    private readonly ILogger<SettingsRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly IFrontolMainDb _mainDb;

    public SettingsRepository(ILogger<SettingsRepository> logger, MainDbCtx ctx, IFrontolMainDb mainDb)
    {
        _logger = logger;
        _ctx = ctx;
        _mainDb = mainDb;
    }

    public async Task<Result> LoadGlobalControlConfig(GlobalControl globalControl)
    {
        if (_ctx.Settings == null)
            return Result.Failure("Не удалось открыть Settings");

        var settings = await AllRows(tracking: true);

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
        
        var settings = await AllRows(tracking: false);

        object? Parser(string raw, Type type) =>
            type == typeof(YesNoWareEnum) ? YesNoWareParsers.ParseYesNoWareEnum(raw) :
            type == typeof(NoWareEnum) ? YesNoWareParsers.ParseNoWareEnum(raw) :
            null;

        var control = settings.ApplyFromSettings<GlobalControl>(Parser);

        return Result.Success(control);
    }

    public async Task<Result> SetSetting(string name, string value)
    {

        if (_ctx.Settings == null)
            return Result.Failure("Не удалось открыть Settings");

        var rows = await _ctx.Settings.Where(s => s.Name == name).ToListAsync();

        if (rows.Count == 0)
        {
            // Старая база Frontol может не содержать строки настройки: без неё значение
            // (например, код стартового скрипта) сохранить некуда — создаём её.
            var newId = await _mainDb.NextChangeId();

            await _ctx.Settings.AddAsync(new Settings
            {
                Id = newId,
                Name = name,
                Value = value
            });

            _logger.LogWarning("Настройка {name} не найдена в таблице SETTINGS — создана со значением {value}", name, value);

            return Result.Success();
        }

        SettingsRows.SetValue(rows, value);

        return Result.Success();
    }

    public async Task<Result<string>> GetSetting(string name)
    {
        if (_ctx.Settings == null)
            return Result.Failure<string>("Не удалось открыть Settings");

        var setting = await _ctx.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Name == name);

        if (setting == null)
            return Result.Failure<string>($"Не удалось установть настройку {name} - не найдена в БД");

        return Result.Success(setting.Value);
    }

    public async Task<Result> LoadParameters(List<FrontolParameter> parameters)
    {
        try
        {
            if (_ctx.Settings == null)
                return Result.Failure("Не удалось открыть Settings");

            var incoming = (parameters ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                .GroupBy(p => p.Id)
                .Select(g => g.Last())
                .ToList();

            if (incoming.Count == 0)
                return Result.Success();

            var rows = await AllRows(tracking: true);

            var rowsByName = SettingsRows.Named(rows)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var parameter in incoming)
            {
                if (!rowsByName.TryGetValue(parameter.Id, out var namedRows))
                {
                    _logger.LogWarning("Настройка {name} не найдена в таблице SETTINGS, пропущена", parameter.Id);
                    continue;
                }

                SettingsRows.SetValue(namedRows, FrontolTimeParsers.ToDb(parameter.Value));
            }

            await _ctx.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            var err = $"Ошибка записи параметров в SETTINGS: {ex.Message}";
            _logger.LogError(ex, err);
            return Result.Failure(err);
        }
    }

    public async Task<Result<List<FrontolParameter>>> GetParameters()
    {
        try
        {
            if (_ctx.Settings == null)
                return Result.Failure<List<FrontolParameter>>("Не удалось открыть Settings");

            var parameters = (await AllRows(tracking: false))
                .Where(row => !string.IsNullOrEmpty(row.Name))
                .Select(row => new FrontolParameter
                {
                    Id = row.Name,
                    Value = FrontolTimeParsers.FromDb(row.Value)
                })
                .ToList();

            return Result.Success(parameters);
        }
        catch (Exception ex)
        {
            var err = $"Ошибка чтения параметров из SETTINGS: {ex.Message}";
            _logger.LogError(ex, err);
            return Result.Failure<List<FrontolParameter>>(err);
        }
    }

    internal IQueryable<Entitys.Settings> AllRowsQuery(bool tracking)
    {
        var query = _ctx.Settings!.AsQueryable();
        return tracking ? query : query.AsNoTracking();
    }

    private Task<List<Entitys.Settings>> AllRows(bool tracking)
        => AllRowsQuery(tracking).ToListAsync();
}