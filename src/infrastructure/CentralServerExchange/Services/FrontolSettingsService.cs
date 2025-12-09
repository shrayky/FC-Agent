using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;
using Domain.Frontol.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange.Services;

public class FrontolSettingsService
{
    private readonly ILogger<FrontolSettingsService> _logger;
    private readonly IServiceScopeFactory  _serviceScope;

    public FrontolSettingsService(ILogger<FrontolSettingsService> logger, IServiceScopeFactory serviceScope)
    {
        _logger = logger;
        _serviceScope = serviceScope;
    }

    public async Task<Result<FrontolSettings>> ReadFrontolSettings()
    {
        using var scope = _serviceScope.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFrontolMainDb>();

        var globalConfigTask = repository.GetGlobalControlConfig();
        var userProfilesTask = repository.GetUserProfiles();

        await Task.WhenAll(globalConfigTask, userProfilesTask);
        
        var globalConfig = globalConfigTask.Result;
        var userProfiles = userProfilesTask.Result;
        
        if (globalConfig.IsFailure)
            return Result.Failure<FrontolSettings>(globalConfigTask.Result.Error);
        
        if  (userProfiles.IsFailure)
            return Result.Failure<FrontolSettings>(userProfilesTask.Result.Error);

        var packet = new FrontolSettings()
        {
            GlobalControl = globalConfig.Value,
            UserProfiles = userProfiles.Value
        };
        
        return Result.Success(packet);
    }
}