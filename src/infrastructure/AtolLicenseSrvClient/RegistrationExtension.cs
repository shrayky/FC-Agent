using Domain.Frontol.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AtolLicenseSrvClient;

public static class RegistrationExtension
{
    public static IServiceCollection AddAtolLicenseSrvClient(this IServiceCollection services)
    {
        services.AddSingleton<IAtolLicenseSource, AtolLicenseService>();
        services.AddSingleton<IAtolLicenseActivator, AtolLicenseActivator>();
        return services;
    }
}
