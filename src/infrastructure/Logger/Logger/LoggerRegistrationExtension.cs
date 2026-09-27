using Domain.Configuration.Constants;
using Domain.Configuration.Options;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Shared.FilesFolders;
using Shared.Logging;
using System.Reflection;
using Shared.DI.Attributes;

namespace Logger
{
    public static class LoggerRegistrationExtension
    {
        public static IServiceCollection AddConfigureLogger(this IServiceCollection services, LogSettings settings)
        {
            // Сборщик логов регистрируется и при выключенном файловом логировании:
            // по нему агент отвечает на команду сервера о своих логах (пакет будет пустым).
            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);

            if (!settings.IsEnabled)
                return services;

            var logFolder = Folders.LogFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            var logFileName = Path.Combine(logFolder, $"{ApplicationInformation.Name.ToLower()}.log");

            services.AddLogging(builder =>
            {
                builder.AddSerilog(SerilogConfiguration.LogToFile(settings.LogLevel, logFileName, settings.LogDepth));
            });

            return services;
        }
    }
}
