using System.Reflection;
using Domain.Configuration.Options;
using Domain.Frontol.Interfaces;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace FrontolDatabase
{
    public static class RegistrationExtension
    {
        public static IServiceCollection AddFrontolDatabase(this IServiceCollection services, DatabaseConnection dbConfig)
        {
            services = ConfigureMainDb(services, dbConfig);
            services = ConfigureLogDb(services, dbConfig);

            services.AddAutoRegisteredServices([Assembly.GetExecutingAssembly()]);
            
            return services;
        }
        
        private static IServiceCollection ConfigureMainDb(IServiceCollection services, DatabaseConnection dbConfig)
        {
            var serverName = @"localhost";
            var databasePath = @"c:\temp\\main.gdb";

            var fullDbPath = dbConfig.DatabasePath.Split(":");

            if (fullDbPath.Length >= 3)
            {
                serverName = fullDbPath[0];
                databasePath = $"{fullDbPath[1]}:{fullDbPath[2]}";
            }
            
            var connectionString = $"Server={serverName};Port=3050;Database={databasePath};User={dbConfig.UserName};Password={dbConfig.Password};";
            
            services.AddDbContext<MainDbCtx>(options =>
                options.UseFirebird(connectionString));
            
            services.AddScoped<IFrontolMainDb, MainDbRepository>();
            
            return services;
        }
        
        private static IServiceCollection ConfigureLogDb(IServiceCollection services, DatabaseConnection dbConfig)
        {
            var serverName = @"localhost";
            var databasePath = @"c:\temp\\log.gdb";

            var fullDbPath = dbConfig.LogDatabasePath.Split(":");

            if (fullDbPath.Length >= 3)
            {
                serverName = fullDbPath[0];
                databasePath = $"{fullDbPath[1]}:{fullDbPath[2]}";
            }
            
            var connectionString = $"Server={serverName};Port=3050;Database={databasePath};User={dbConfig.UserName};Password={dbConfig.Password};";
            
            services.AddDbContext<LogDbCtx>(options =>
                options.UseFirebird(connectionString));
            
            services.AddScoped<IFrontolLog, LogRepository>();
            
            return services;
        }
        
    }
}
