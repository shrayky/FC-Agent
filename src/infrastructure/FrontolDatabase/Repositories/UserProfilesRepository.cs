using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;
using Domain.Frontol.Interfaces;
using FrontolDatabase.Entitys;
using FrontolDatabase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class UserProfilesRepository : IFrontolUserProfiles
{
    private readonly ILogger<UserProfilesRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly UserProfileDefaultSecurityService _defaultSecurityService;
    private readonly IFrontolMainDb _mainDb;

    public UserProfilesRepository(ILogger<UserProfilesRepository> logger, MainDbCtx ctx, UserProfileDefaultSecurityService defaultSecurityService, IFrontolMainDb mainDb)
    {
        _logger = logger;
        _ctx = ctx;
        _defaultSecurityService = defaultSecurityService;
        _mainDb = mainDb;
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

        var defaultSecurityCodes = await _defaultSecurityService.GetAllSecurityCodesAsync();
        if (defaultSecurityCodes.Count == 0)
            _logger.LogWarning("Не удалось загрузить коды Securities из JSON файла");

        foreach (var profile in userProfiles)
        {
            var existProfile = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.Code == profile.Code);

            if (existProfile == null)
            {
                var newProfileCreateResult = await CreateNewProfile(profile, defaultSecurityCodes);

                if (newProfileCreateResult.IsFailure)
                {
                    _logger.LogWarning("Ошибка создания профиля {code} в базу данных фронтол: {err}", profile.Code, newProfileCreateResult.Error);
                    continue;
                }

                existProfile = newProfileCreateResult.Value;
            }
            
            var profileUpdateResult = await UpdateProfile(existProfile, profile);

            if (profileUpdateResult.IsFailure)
            {
                _logger.LogWarning("Ошибка записи профиля {code} в базу данных фронтол: {err}",  profile.Code, profileUpdateResult.Error);
            }
        }

        return Result.Success();

    }

    private async Task<Result<Profile>> CreateNewProfile(UserProfile profile, HashSet<int> securityCodes)
    {
        if (_ctx.UserProfiles == null)
            return Result.Failure<Profile>("База данных UserProfiles недоступна");
        
        if (_ctx.UserProfileSecurity == null)
            return Result.Failure<Profile>("База данных UserProfileSecurity недоступна");
        
        var userProfile = new Profile
        {
            Id = await _mainDb.NextChangeId(),
            Code = profile.Code,
            Name = profile.Name,
            DontChangeUsersOnExchange = profile.DontLoadUserWithThisProfile,
            ForSelfieUser = profile.ForSelfieMode,
            SkipSupervisorMode = profile.SkipSupervisorMode,
        };

        try
        {
            await _ctx.UserProfiles.AddAsync(userProfile);
        }
        catch (Exception ex)
        {
            return Result.Failure<Profile>(ex.Message);
        }
        
        var defaultSecurities= await _defaultSecurityService.CreateDefaultSecuritiesAsync(userProfile.Id, securityCodes);

        List<Security> profileSecurites = [];

        foreach (var security in defaultSecurities)
        {
            Security row = new()
            {
                Id = await _mainDb.NextChangeId(),
                ProfileId = userProfile.Id,
                SecurityCode = security.SecurityCode,
                Value = security.Value
            };

            profileSecurites.Add(row);
        }

        try
        {
            await _ctx.UserProfileSecurity.AddRangeAsync(profileSecurites);
        }
        catch (Exception ex)
        {
            return Result.Failure<Profile>(ex.Message);
        }

        try
        {
            await _ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return Result.Failure<Profile>(ex.Message);
        }

        return Result.Success(userProfile);
    }
    
    private async Task<Result<int>> UpdateProfile(Profile existProfile, UserProfile profile)
    {
        if (_ctx.UserProfiles == null)
            return Result.Failure<int>("База данных UserProfiles недоступна");
        
        if (_ctx.UserProfileSecurity == null)
            return Result.Failure<int>("База данных UserProfileSecurity недоступна");
        
        existProfile.Name = profile.Name;
        existProfile.DontChangeUsersOnExchange = profile.DontLoadUserWithThisProfile;
        existProfile.ForSelfieUser = profile.ForSelfieMode;
        existProfile.SkipSupervisorMode = profile.SkipSupervisorMode;
        try
        {
            _ctx.UserProfiles.Update(existProfile);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(ex.Message);
        }

        var existSecurities = await _ctx.UserProfileSecurity
            .Where(f => f.ProfileId == existProfile.Id)
            .ToListAsync();
        
        var existingSecurityCodes = existSecurities.Select(s => s.SecurityCode).ToHashSet();
        
        var newSecuritiesToAdd = profile.Securities
            .Where(ps => !existingSecurityCodes.Contains(ps.Id))
            .ToList();

        var newSecurities = new List<Security>();
        
        foreach (var ps in newSecuritiesToAdd)
        {
            var security = new Security
            {
                Id = await _mainDb.NextChangeId(),
                ProfileId = existProfile.Id,
                SecurityCode = ps.Id,
                Value = ps.Value
            };
            newSecurities.Add(security);
        }

        var securitiesToUpdate = SecuritiesToUpdate(profile.Securities, existSecurities); 
                    
        foreach (var secToUpdate in securitiesToUpdate)
        {
            var newValue = profile.Securities.First(ps => ps.Id == secToUpdate.SecurityCode);
            secToUpdate.Value = newValue.Value;
            
            _ctx.UserProfileSecurity.Update(secToUpdate);
        }
        
        if (newSecurities.Count > 0)
        {
            await _ctx.UserProfileSecurity.AddRangeAsync(newSecurities);
        }
        
        try
        {
            await _ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(ex.Message);
        }
        
        return Result.Success(existProfile.Id);
    }
    
    internal List<Security> SecuritiesToUpdate(List<UserProfileSecurity> profileSecurities, List<Security> existSecurities)
    {
        return profileSecurities
            .Join(existSecurities, profileSec => profileSec.Id, existSec => existSec.SecurityCode,
                (profileSec, existSec) => new { profileSec, existSec })
            .Where(t => t.profileSec.Value != t.existSec.Value)
            .Select(t => t.existSec)
            .ToList();
    }
    
}