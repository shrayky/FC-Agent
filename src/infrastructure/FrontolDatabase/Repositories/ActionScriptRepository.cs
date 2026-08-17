using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class ActionScriptRepository : IFrontolActionScripts
{
    private readonly ILogger<ActionScriptRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly IFrontolMainDb _mainDb;
    private readonly IFrontolSettings _settings;

    const string ScriptSettingName = "StartScript";

    public ActionScriptRepository(ILogger<ActionScriptRepository> logger, MainDbCtx ctx, IFrontolMainDb mainDb, IFrontolSettings settings)
    {
        _logger = logger;
        _ctx = ctx;
        _mainDb = mainDb;
        _settings = settings;
    }

    public async Task<Result<ActionScript>> FromDb()
    {
        if (_ctx.ActionScripts == null)
            return Result.Failure<ActionScript>("База данных ActionScripts недоступна");

        var scritSetting = await _settings.GetSetting(ScriptSettingName);

        if (scritSetting.IsFailure)
        {
            _logger.LogError(scritSetting.Error);
            return Result.Failure<ActionScript>(scritSetting.Error);
        }

        int scriptCode = int.Parse(scritSetting.Value);

        var answer = new ActionScript();

        if (scriptCode == 0)
           return Result.Success(answer);

        var entity = await _ctx.ActionScripts.AsNoTracking().FirstOrDefaultAsync(p => p.Code == scriptCode);

        if (entity == null)
            return Result.Success(answer);

        answer.Code = scriptCode;
        answer.Text = entity.Script;

        return Result.Success(answer);
    }

    public async Task<Result> ToDb(ActionScript actionScript)
    {
        if (_ctx.ActionScripts == null)
            return Result.Failure("База данных ActionScripts недоступна");

        var entity = await _ctx.ActionScripts.AsNoTracking().FirstOrDefaultAsync<ActScript>(p => p.Code == actionScript.Code);

        if (entity == null)
        {
            entity = new()
            {
                Code = actionScript.Code,
                Name = actionScript.Name,
                Script = actionScript.Text,
                Id = await _mainDb.NextChangeId()
            };

            _ctx.ActionScripts.Add(entity);
        }
        else
        {
            entity.Name = actionScript.Name;
            entity.Script = actionScript.Text;

            _ctx.ActionScripts.Update(entity);
        }

        var setSettingResult = await _settings.SetSetting(ScriptSettingName, entity.Code.ToString());

        if (setSettingResult.IsFailure)
        {
            _logger.LogError(setSettingResult.Error);
            return Result.Failure(setSettingResult.Error);
        }

        try
        {
            await _ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var err = $"Ошибка при сохранении ActionScripts: {ex}";

            _logger.LogError(err);
            return Result.Failure(err);
        }

        return Result.Success();
    }
}
