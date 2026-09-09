using System.Net;
using CentralServerExchange.Services;
using CentralServerExchange.Workers;
using Domain.Agent.Interfaces;
using Domain.Sales;
using Domain.Sales.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CentralServerExchange
{
    public static class RegistrationExtension
    {
        public static IServiceCollection AddCentralServerClient(this IServiceCollection services)
        {

            var updaterClient = services.AddHttpClient<AgentUpdateService>("UpdateDownloader", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30);
                if (!ForceHttp11MessageHandler.IsRequiredOnThisOs)
                    return;

                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            });

            if (ForceHttp11MessageHandler.IsRequiredOnThisOs)
            {
                updaterClient.ConfigurePrimaryHttpMessageHandler(
                    () => new ForceHttp11MessageHandler(new SocketsHttpHandler()));
            }
            
            services.AddSingleton<FrontolStateService>();
            services.AddSingleton<AtolLicenseService>();
            services.AddSingleton<AgentUpdateService>();
            services.AddSingleton<FrontolLogsService>();
            services.AddSingleton<FrontolSettingsService>();
            services.AddSingleton<IFcRemoteProcessSource, WindowsFcRemoteProcessSource>();
            
            services.AddSingleton<ISalesCursorState, SalesCursorState>();
            services.AddSingleton<SignalRAgentClient>();
            
            services.AddHostedService<ExchangeWorker>();
            services.AddHostedService<UpdateDownloadWorker>();
            services.AddHostedService<SalesSyncWorker>();

            return services;
        }
    }
}
