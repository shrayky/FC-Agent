using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Metadata;
using FrontolDatabase.Entitys;
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
        
        await Restart();

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

    public async Task<Result<List<UserProfile>>> GetUserProfiles()
    {
        if (_ctx.UserProfiles == null)
            return Result.Failure<List<UserProfile>>("Не удалось открыть UserProfiles");
        
        if (_ctx.UserProfileSecurity == null)
            return Result.Failure<List<UserProfile>>("Не удалось открыть UserProfileSecurity");

        var profiles = await _ctx.UserProfiles.AsNoTracking().ToListAsync();
        var security = await _ctx.UserProfileSecurity.AsNoTracking().ToListAsync();

        List<UserProfile> answer = [];

        foreach (var profileData in profiles)
        {
            var profileSecurity = security.Where(p => p.ProfileId == profileData.Id && p.Value == 1)
                .Select(s => new UserProfileSecurity
                {
                    Id = s.SecurityCode,
                    Value = s.Value,
                    Name = string.Empty
                })
                .ToList();
            
            var profile = new UserProfile()
            {
                Code = profileData.Code,
                Name = profileData.Name,
                DontLoadUserWithThisProfile = profileData.DontChangeUsersOnExchange,
                ForSelfieMode = profileData.ForSelfieUser,
                SkipSupervisorMode = profileData.SkipSupervisorMode,
                Securities = profileSecurity,
            };
            
            answer.Add(profile);
        }
        
        return Result.Success(answer);
    }

    public async Task<Result> LoadUserProfiles(List<UserProfile> userProfiles)
    {
        if (_ctx.UserProfiles == null)
            return Result.Failure("Не удалось открыть UserProfiles");
        
        if (_ctx.UserProfileSecurity == null)
            return Result.Failure("Не удалось открыть UserProfileSecurity");

        foreach (var userProfile in userProfiles)
        {
            var existProfile = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.Code == userProfile.Code);

            if (existProfile is null)
            {
                Profile profile = new()
                {
                    Code = userProfile.Code,
                    Name = userProfile.Name,
                    DontChangeUsersOnExchange = userProfile.DontLoadUserWithThisProfile,
                    ForSelfieUser = userProfile.ForSelfieMode,
                    SkipSupervisorMode = userProfile.SkipSupervisorMode,
                };
                
                await _ctx.UserProfiles.AddAsync(profile);
            }
            else
            {
                existProfile.Name = userProfile.Name;
                existProfile.DontChangeUsersOnExchange = userProfile.DontLoadUserWithThisProfile;
                existProfile.ForSelfieUser = userProfile.ForSelfieMode;
                existProfile.SkipSupervisorMode = userProfile.SkipSupervisorMode;
                
                _ctx.UserProfiles.Update(existProfile);
            }
            
            await _ctx.SaveChangesAsync();
            
            var profileId = _ctx.UserProfiles.FirstOrDefault(p => p.Code == userProfile.Code)?.Id;
            
            if (profileId == null)
                continue;
            
            var existSecurities = await _ctx.UserProfileSecurity.Where(p => p.ProfileId == profileId).AsNoTracking().ToListAsync();

            foreach (var existSecurity in existSecurities)
            {
                var security = userProfile.Securities.Find(p => p.Id == existSecurity.SecurityCode);

                if (security is null)
                {
                    var newSecurity = new Security()
                    {
                        ProfileId = profileId ?? 0,
                        SecurityCode = existSecurity.SecurityCode,
                        Value = existSecurity.Value
                    };
                    
                    _ctx.UserProfileSecurity.Add(newSecurity);
                }
                else
                {
                    if (security.Value == existSecurity.Value)
                        continue;
                    
                    existSecurity.Value =security.Value;
                    
                    _ctx.UserProfileSecurity.Update(existSecurity);
                }
            }
            
            await _ctx.SaveChangesAsync();
        }
        
        await Restart();
        
        return Result.Success();
    }
}