using Domain.Configuration.Constants;
using Shared.Installer;

namespace ViewApp.Services;

public static class StartService
{
    public static void ProcessStartWithArguments(string[] args, int ipPort)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("Использование:");
            Console.WriteLine("--install - для установки службы (для linux - генерация скриптов установки)");
            Console.WriteLine("--uninstall - для удаления службы (для linux - генерация скриптов удаления)");
            return;
        }
        
        if (args.Contains("--install"))
        {
            InstallerFabric.Install(args, 
                $"{ApplicationInformation.Name}",
                $"{ApplicationInformation.ServiceName}",
                ApplicationInformation.Manufacture,
                ipPort);
        }
        else if (args.Contains("--uninstall"))
        {
            InstallerFabric.Uninstall($"{ApplicationInformation.Name}",
                $"{ApplicationInformation.ServiceName}", 
                ApplicationInformation.Manufacture, 
                ipPort);
        }
    }
}