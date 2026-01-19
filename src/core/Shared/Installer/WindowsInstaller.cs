using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Shared.FilesFolders;
using Shared.Installer.Interface;
using static System.Console;
using static System.ServiceProcess.ServiceController;

namespace Shared.Installer;

[SupportedOSPlatform("windows")]
public class WindowsInstaller : IInstaller
{
    private readonly string _appName;
    private readonly string _serviceName;
    private readonly string _manufacture;
    private readonly int _serviceIpPort;

    public WindowsInstaller(string appName, string serviceName, string manufacture, int serviceIpPort)
    {
        _appName = appName;
        _serviceName = serviceName;
        _manufacture = manufacture;
        _serviceIpPort = serviceIpPort;
    }

    public void Install(string[] installerArgs)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        var installDirectory = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "",
            "Program Files",
            _manufacture,
            _appName);
        
        if (!Directory.Exists(installDirectory))
            Directory.CreateDirectory(installDirectory);

        var installerFileName = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var exeName = Path.GetFileName(installerFileName);
        var setupFolder = Path.GetDirectoryName(installerFileName) ?? installerFileName.Replace(exeName, "");
        
        var binPath = Path.Combine(installDirectory, exeName);
        var wwwrootPath = Path.Combine(installDirectory, "wwwroot");

        StopService();

        Thread.Sleep(TimeSpan.FromMinutes(1));            
        
        if (File.Exists(binPath))
            File.Delete(binPath);
        
        if (Directory.Exists(wwwrootPath))
            Directory.Delete(wwwrootPath, true);
        
        Folders.CopyDirectory(setupFolder, installDirectory);

        CreateService(binPath);
        
        StartService();
    }
    
    public void Uninstall()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        var installDirectory = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "",
            "Program Files",
            _manufacture,
            _appName);
        
        if (!Directory.Exists(installDirectory))
            return;

        var exeName = $"{_appName}.exe";
        
        var binPath = Path.Combine(installDirectory, exeName);
        var wwwrootPath = Path.Combine(installDirectory, "wwwroot");

        StopService();

        RemoveService();
        
        if (File.Exists(binPath))
            File.Delete(binPath);
        
        if (Directory.Exists(wwwrootPath))
            Directory.Delete(wwwrootPath, true);
    }

    private void RemoveService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _appName);

        if (existingService == null) 
        {
            WriteLine($"Служба {_appName} не существует");
            return;
        }
        
        Process process = new();
        ProcessStartInfo startInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden
        };

        process.StartInfo = startInfo;
        startInfo.FileName = "cmd.exe";

        startInfo.Arguments = $"/c sc delete {_appName}";
        process.Start();
        
        startInfo.Arguments = $"/c netsh advfirewall firewall delete rule name = \"{_appName}\"";
        process.Start();
    }

    private void CreateService(string bin)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _appName);

        Process process = new();
        ProcessStartInfo startInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden
        };

        process.StartInfo = startInfo;
        startInfo.FileName = "cmd.exe";

        
        if (existingService == null)
        {
            startInfo.Arguments = $"/c sc create {_appName} binPath= \"{bin}\" DisplayName= \"{_serviceName}\" type= own start= auto depend= FirebirdServerDefaultInstance";
            process.Start();
            
            startInfo.Arguments = $"/c sc failure \"{_appName}\" reset= 5 actions= restart/5000";
            process.Start();
        }
        else
        {
            WriteLine($"Служба {_appName} уже существует");
        }

        startInfo.Arguments = $"/c netsh advfirewall firewall delete rule name = \"{_serviceName}\"";
        process.Start();

        if (_serviceIpPort != 0)
        {
            startInfo.Arguments =
                $"/c netsh advfirewall firewall add rule name = \"{_serviceName}\" dir =in action = allow protocol = TCP localport = {_serviceIpPort}";
            process.Start();
        }

        const string taskName = "fc-guard";
        
        startInfo.Arguments = $"/c schtasks /Delete /TN \"{taskName}\" /F 2>nul";
        process.Start();
        
        var cmdCommand = $"sc query {_appName} | find \"RUNNING\" >nul || sc start {_appName}";
        var escapedCommand = cmdCommand.Replace("\"", "\\\"");
        
        startInfo.Arguments = $"/c schtasks /Create /TN \"{taskName}\" /TR \"cmd.exe /c {escapedCommand}\" /SC MINUTE /MO 5 /F /RL HIGHEST";
        process.Start();

        startInfo.Arguments = $"/c net start {_appName}";
        process.Start();
    }
    
    private void StopService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        
        existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _appName);

        if (existingService == null)
        {
            WriteLine($"Не существует службы {_appName}");
            return;
        }

        if (existingService.Status != ServiceControllerStatus.Running)
        {   
            WriteLine($"Служба {_appName} не выполняется");
            return;
        }

        existingService.Stop();
        existingService.WaitForStatus(ServiceControllerStatus.Stopped);
        
        WriteLine($"Служба {_appName}, остановлена");
    }
    
    private void StartService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        var existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _appName);

        if (existingService == null)
        {
            WriteLine($"Не удалось запустить службу {_appName} - ее не существует");
            return;
        }

        if (existingService.Status == ServiceControllerStatus.Running)
        {
            WriteLine($"Служба {_appName} уже запущена");
            return;
        }

        existingService.Start();
        existingService.WaitForStatus(ServiceControllerStatus.Running);
        
        WriteLine($"Служба {_appName}, запущена", _appName);
    }
    
    private bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName.Replace(".exe", ""));
        return processes.Length > 0;
    }
    
    private void WaitForProcessToExit(string processName, int maxWaitSeconds = 5)
    {
        var startTime = DateTime.Now;
        var maxWaitTime = TimeSpan.FromSeconds(maxWaitSeconds);
    
        while (IsProcessRunning(processName))
        {
            if (DateTime.Now - startTime > maxWaitTime)
            {
                WriteLine($"Процесс {processName} не завершился за {maxWaitSeconds} секунд");
                break;
            }
        
            Thread.Sleep(100);
        }
    }
}