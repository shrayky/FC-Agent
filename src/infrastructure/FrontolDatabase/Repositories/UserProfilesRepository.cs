using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;
using Domain.Frontol.Interfaces;
using FrontolDatabase;
using FrontolDatabase.Entitys;
using FrontolDatabase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class UserProfilesRepository : IFrontolUserProfiles
{
    private readonly ILogger<UserProfilesRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly UserProfileDefaultSecurityService _defaultSecurityService;
    private readonly IFrontolMainDb _maindDb;

    public UserProfilesRepository(ILogger<UserProfilesRepository> logger, MainDbCtx ctx, UserProfileDefaultSecurityService defaultSecurityService, IFrontolMainDb maindDb)
    {
        _logger = logger;
        _ctx = ctx;
        _defaultSecurityService = defaultSecurityService;
        _maindDb = maindDb;
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
                
            }

        }
        

        try
        {
            foreach (var userProfile in userProfiles)
            {
                var existProfile = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.Code == userProfile.Code);

                if (existProfile is null)
                {
                    Profile profile = new()
                    {
                        Id = await _maindDb.NextChangeId(),
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