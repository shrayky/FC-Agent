using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using FrontolDatabase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class MainDbRepository : IFrontolMainDb
{
    private readonly ILogger<MainDbCtx> _logger;
    private readonly MainDbCtx _ctx;
    private readonly UserProfileDefaultSecurityService _userProfileDefaultSecurityService;

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
}