using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using FrontolDatabase.Entitys;
using FrontolDatabase.Mapping;
using FrontolDatabase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class MainDbRepository : IFrontolMainDb
{
    private readonly ILogger<MainDbCtx> _logger;
    private readonly MainDbCtx _ctx;
    private readonly UserProfileDefaultSecurityService _userProfileDefaultSecurityService;
    private IDbContextTransaction? _write;

    public MainDbRepository(ILogger<MainDbCtx> logger, MainDbCtx ctx, UserProfileDefaultSecurityService userProfileDefaultSecurityService)
    {
        _logger = logger;
        _ctx = ctx;
        _userProfileDefaultSecurityService = userProfileDefaultSecurityService;
    }
    
    public async Task<int> NextChangeId()
    {
        var connection = _ctx.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
    
        if (!wasOpen)
            await connection.OpenAsync();
    
        try
        {
            await using var command = connection.CreateCommand();
            // Команда на соединении с открытой транзакцией должна идти в ней же.
            var current = _ctx.Database.CurrentTransaction;
            if (current != null)
                command.Transaction = current.GetDbTransaction();

            command.CommandText = "SELECT GEN_ID(GCHNG, 1) FROM RDB$DATABASE";
        
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0);
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
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

            var text = newVal.ToString();
            var rows = await _ctx.Settings.Where(s => s.Name == restartOption).ToListAsync();

            if (rows.Count == 0)
            {
                _ctx.Settings.Add(new Settings
                {
                    Id = newVal,
                    Name = restartOption,
                    Value = text
                });
            }
            else
            {
                SettingsRows.SetValue(rows, text);
            }

            await _ctx.SaveChangesAsync();
        
            return Result.Success();
        }
        catch (Exception e)
        {
            _logger.LogError("Ошибка записи настройки {restartOption} {ex}", restartOption, e.Message);
            return Result.Failure(e.Message);
        }
    }

    public async Task<Result> BeginWrite()
    {
        if (_write != null)
            return Result.Failure("Запись в базу Frontol уже начата");

        try
        {
            _write = await _ctx.Database.BeginTransactionAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось начать запись в базу Frontol");
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> CommitWrite()
    {
        if (_write == null)
            return Result.Failure("Нет открытой записи в базу Frontol");

        try
        {
            await _write.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось зафиксировать запись в базу Frontol");
            return Result.Failure(ex.Message);
        }
        finally
        {
            await DisposeWrite();
        }
    }

    public async Task RollbackWrite()
    {
        if (_write == null)
            return;

        try
        {
            await _write.RollbackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось откатить запись в базу Frontol");
        }
        finally
        {
            await DisposeWrite();
        }
    }

    private async Task DisposeWrite()
    {
        if (_write == null)
            return;

        var write = _write;
        _write = null;
        await write.DisposeAsync();
    }
}