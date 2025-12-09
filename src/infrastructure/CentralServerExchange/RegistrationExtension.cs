using CentralServerExchange.Services;
using CentralServerExchange.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace CentralServerExchange
{
    public static class RegistrationExtension
    {
        public static IServiceCollection AddCentralServerClient(this IServiceCollection services)
        {

            services.AddHttpClient<AgentUpdateService>("UpdateDownloader", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30);
            });
            
            services.AddSingleton<FrontolStateService>();
            services.AddSingleton<AtolLicenseService>();
            services.AddSingleton<AgentUpdateService>();
            services.AddSingleton<FrontolLogsService>();
            services.AddSingleton<FrontolSettingsService>();
            
            services.AddSingleton<SignalRAgentClient>();
            
            services.AddHostedService<ExchangeWorker>();
            services.AddHostedService<UpdateDownloadWorker>();

            return services;
        }
    }
}
