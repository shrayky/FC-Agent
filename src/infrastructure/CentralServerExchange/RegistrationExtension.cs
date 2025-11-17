using CentralServerExchange.Services;
using CentralServerExchange.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace CentralServerExchange
{
    public static class RegistrationExtension
    {
        public static IServiceCollection AddCentralServerClient(this IServiceCollection services)
        {
            services.AddSingleton<FrontolStateService>();
            services.AddSingleton<SignalRAgentClient>();
            
            services.AddHostedService<ExchangeWorker>();

            return services;
        }
    }
}
