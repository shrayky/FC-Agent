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

        var globalConfig = await repository.GetGlobalControlConfig();
        var userProfiles = await repository.GetUserProfiles();
        
        if (globalConfig.IsFailure)
            return Result.Failure<FrontolSettings>(globalConfig.Error);
        
        if  (userProfiles.IsFailure)
            return Result.Failure<FrontolSettings>(userProfiles.Error);

        var packet = new FrontolSettings()
        {
            GlobalControl = globalConfig.Value,
            UserProfiles = userProfiles.Value
        };
        
        return Result.Success(packet);
    }
}