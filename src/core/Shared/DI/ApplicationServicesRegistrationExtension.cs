using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.DI
{
    public static class ApplicationServicesRegistrationExtension
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

            return services;
        }
    }
}
