using CSharpFunctionalExtensions;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Metadata;
using Domain.Frontol.Models.Settings;
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

    public async Task<Result> SetSetting(string name, string value)
    {

        if (_ctx.Settings == null)
            return Result.Failure("Не удалось открыть Settings");

        var setting = await _ctx.Settings.FirstOrDefaultAsync(s => s.Name == name);

        if (setting == null)
            return Result.Failure($"Не удалось установть настройку {name} - не найдена в БД");

        setting.Value = value;

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

            var names = incoming.Select(p => p.Id).ToArray();
            var rows = await _ctx.Settings
                .Where(s => names.Contains(s.Name))
                .ToListAsync();

            var rowsByName = rows
                .GroupBy(s => s.Name)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var parameter in incoming)
            {
                if (!rowsByName.TryGetValue(parameter.Id, out var row))
                {
                    _logger.LogWarning("Настройка {name} не найдена в таблице SETTINGS, пропущена", parameter.Id);
                    continue;
                }

                row.Value = FrontolTimeParsers.ToDb(parameter.Value);
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

            var rows = await _ctx.Settings
                .AsNoTracking()
                .Where(s => s.Name != "")
                .Select(s => new { s.Name, s.Value })
                .ToListAsync();

            var parameters = rows.Select(s => new FrontolParameter
            {
                Id = s.Name,
                Value = FrontolTimeParsers.FromDb(s.Value)
            }).ToList();

            return Result.Success(parameters);
        }
        catch (Exception ex)
        {
            var err = $"Ошибка чтения параметров из SETTINGS: {ex.Message}";
            _logger.LogError(ex, err);
            return Result.Failure<List<FrontolParameter>>(err);
        }
    }
}