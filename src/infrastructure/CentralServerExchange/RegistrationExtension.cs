using System.Net;
using CentralServerExchange.Services;
using CentralServerExchange.Workers;
using Domain.Agent.Interfaces;
using Domain.Frontol.Interfaces;
using Domain.Sales;
using Domain.Sales.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange
{
    public static class RegistrationExtension
    {
        private const string UpdateDownloaderClientName = "UpdateDownloader";

        public static IServiceCollection AddCentralServerClient(this IServiceCollection services)
        {
            // Именованный, а не типизированный клиент: AgentUpdateService регистрируется ниже как singleton,
            // и типизированная регистрация (transient) была бы им перекрыта — тогда в загрузчик обновлений
            // попадал бы безымянный HttpClient без TLS 1.2 и с таймаутом 100 секунд по умолчанию.
            var updaterClient = services.AddHttpClient(UpdateDownloaderClientName, client =>
            {
                // Архив обновления ~8 МБ, и загрузка докачивается с текущей позиции,
                // поэтому таймаут ограничивает только одну попытку, а не всю загрузку.
                client.Timeout = TimeSpan.FromMinutes(5);
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

            services.AddSingleton(sp => new AgentUpdateService(
                sp.GetRequiredService<ILogger<AgentUpdateService>>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(UpdateDownloaderClientName)));

            services.AddSingleton<FrontolStateService>();
            services.AddSingleton<AtolLicenseService>();
            services.AddSingleton<IAtolLicenseActivator, AtolLicenseActivator>();
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
