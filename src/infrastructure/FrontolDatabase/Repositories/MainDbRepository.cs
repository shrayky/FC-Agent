using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Metadata;
using FrontolDatabase.Entitys;
using FrontolDatabase.Mapping;
using FrontolDatabase.Parsers;
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
    
    private async Task<int> NextChangeId()
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

        try
        {
            var defaultSecurityCodes = await _userProfileDefaultSecurityService.GetAllSecurityCodesAsync();
            if (defaultSecurityCodes.Count == 0)
                _logger.LogWarning("Не удалось загрузить коды Securities из JSON файла");

            foreach (var userProfile in userProfiles)
            {
                var existProfile = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.Code == userProfile.Code);

                if (existProfile is null)
                {
                    Profile profile = new()
                    {
                        Id = await NextChangeId(),
                        Code = userProfile.Code,
                        Name = userProfile.Name,
                        DontChangeUsersOnExchange = userProfile.DontLoadUserWithThisProfile,
                        ForSelfieUser = userProfile.ForSelfieMode,
                        SkipSupervisorMode = userProfile.SkipSupervisorMode,
                    };

                    await _ctx.UserProfiles.AddAsync(profile);
                    await _ctx.SaveChangesAsync();

                    var profileId = profile.Id;

                    var securitiesToAdd = new List<Security>();
                    
                    foreach (var securityCode in defaultSecurityCodes)
                    {
                        var securityValue = userProfile.Securities.Any(s => s.Id == securityCode) ? 1 : 0;

                        var newSecurity = new Security()
                        {
                            ProfileId = profileId,
                            SecurityCode = securityCode,
                            Value = securityValue
                        };

                        securitiesToAdd.Add(newSecurity);
                    }
                    
                    await _ctx.UserProfileSecurity.AddRangeAsync(securitiesToAdd);
                }
                else
                {
                    existProfile.Name = userProfile.Name;
                    existProfile.DontChangeUsersOnExchange = userProfile.DontLoadUserWithThisProfile;
                    existProfile.ForSelfieUser = userProfile.ForSelfieMode;
                    existProfile.SkipSupervisorMode = userProfile.SkipSupervisorMode;

                    var profileId = existProfile.Id;

                    var existSecurities = await _ctx.UserProfileSecurity
                        .Where(p => p.ProfileId == profileId)
                        .AsNoTracking()
                        .ToListAsync();

                    var userProfileSecuritiesDict = userProfile.Securities.ToDictionary(s => s.Id, s => s.Value);

                    var securitiesToAdd = new List<Security>();
                    
                    foreach (var securityCode in defaultSecurityCodes)
                    {
                        var existSecurity = existSecurities.FirstOrDefault(s => s.SecurityCode == securityCode);
    
                        if (existSecurity != null)
                        {
                            var trackedSecurity = await _ctx.UserProfileSecurity
                                .FirstOrDefaultAsync(s => s.ProfileId == profileId && s.SecurityCode == securityCode);
        
                            if (trackedSecurity != null)
                                trackedSecurity.Value = userProfileSecuritiesDict.TryGetValue(securityCode, out var value) ? value : 0;
                        }
                        else
                        {
                            var newValue = userProfileSecuritiesDict.TryGetValue(securityCode, out var value) ? value : 0;
                            securitiesToAdd.Add(new Security()
                            {
                                SecurityCode = securityCode,
                                Value = newValue
                            });
                        }
                    }

                    var existingCodes = existSecurities.Select(s => s.SecurityCode).ToHashSet();
                    foreach (var security in userProfile.Securities)
                    {
                        if (existingCodes.Contains(security.Id))
                            continue;

                        var newSecurity = new Security()
                        {
                            ProfileId = profileId,
                            SecurityCode = security.Id,
                            Value = security.Value
                        };

                        await _ctx.UserProfileSecurity.AddAsync(newSecurity);
                    }
                }

                await _ctx.SaveChangesAsync();
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            var err = $"Ошибка загрузки профилей пользователей: {ex.Message}";
            _logger.LogError(ex, err);
            return Result.Failure(err);
        }
    }
}