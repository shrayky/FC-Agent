using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;
using DotNetHost;
using HostApp.Services;

namespace HostApp.Installer;

[SupportedOSPlatform("windows")]
internal sealed class WindowsHostInstaller
{
    private readonly string _installDirectory;
    private readonly string _logFilePath;

    public WindowsHostInstaller()
    {
        _installDirectory = Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
            "Program Files",
            HostConstants.Manufacture,
            HostConstants.AppName);

        Directory.CreateDirectory(HostPaths.DataFolder);
        _logFilePath = Path.Combine(HostPaths.DataFolder, "updateLog.txt");
    }

    public async Task<int> InstallAsync(string[] args)
    {
        StartLog("install");
        LogInfo($"Аргументы: {string.Join(' ', args.Select(a => $"\"{a}\""))}");
        LogInfo($"Каталог установки: {_installDirectory}");

        try
        {
            await WaitForSourceProcessAsync(args);

            Directory.CreateDirectory(_installDirectory);

            StopAndKillService();
            RemoveLegacyInstall();

            var setupFolder = GetSetupFolder();
            LogInfo($"Каталог пакета: {setupFolder}");

            await EnsureAspNetRuntimeAsync(setupFolder);

            await InstallHostAsync(setupFolder);
            await InstallProductVersionsAsync(setupFolder);

            EnsureServiceRegistered();
            WriteChecksum(args);
        }
        catch (Exception ex)
        {
            LogError($"Ошибка установки: {ex}");
            return 1;
        }

        StartService();
        LogInfo("Установка завершена успешно.");
        return 0;
    }

    /// <summary>
    /// Удаляет службу и host; каталоги fc-agent/fc-remote не трогает.
    /// </summary>
    public int Uninstall()
    {
        StartLog("uninstall");

        try
        {
            Unregister();
            RemoveLegacyInstall();

            var hostPath = Path.Combine(_installDirectory, HostConstants.HostExeName);
            DeleteFileWithRetry(hostPath);

            DeleteDirectoryWithRetry(Path.Combine(_installDirectory, "wwwroot"));

            LogInfo("Удаление завершено. Каталоги продуктов сохранены.");
            return 0;
        }
        catch (Exception ex)
        {
            LogError($"Ошибка удаления: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Регистрирует службу без копирования файлов.
    /// </summary>
    public int Register()
    {
        StartLog("register");

        try
        {
            EnsureServiceRegistered();
        }
        catch (Exception ex)
        {
            LogError($"Ошибка регистрации: {ex}");
            return 1;
        }

        StartService();
        LogInfo("Регистрация завершена.");
        return 0;
    }

    /// <summary>
    /// Удаляет службу из SCM, файлы не трогает.
    /// </summary>
    public int Unregister()
    {
        StartLog("unregister");

        try
        {
            StopAndKillService();

            RunCmdAllowFailure($"sc delete {HostConstants.ServiceName}");
            RunCmdAllowFailure($"netsh advfirewall firewall delete rule name = \"{HostConstants.ServiceName}\"");
            RemoveServiceGuardTask();

            LogInfo("Служба удалена из SCM.");
            return 0;
        }
        catch (Exception ex)
        {
            LogError($"Ошибка unregister: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Ставит ASP.NET Core Runtime из runtime\*.exe до копирования FDD-продуктов.
    /// </summary>
    private async Task EnsureAspNetRuntimeAsync(string setupFolder)
    {
        var installer = AspNetRuntimeSetup.FindInstaller(setupFolder);
        if (installer is null)
        {
            LogInfo("Установщик ASP.NET Runtime в пакете отсутствует — шаг пропущен.");
            return;
        }

        var installed = InstalledDotNetRuntimes.ListFromWindows();
        if (!AspNetRuntimeSetup.NeedsInstall(installed))
        {
            LogInfo("Microsoft.AspNetCore.App 10 уже установлен — установщик не запускаем.");
            return;
        }

        LogInfo($"Устанавливаю ASP.NET Runtime: {installer}");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = installer,
            Arguments = "/install /quiet /norestart",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        if (!process.Start())
            throw new InvalidOperationException("Не удалось запустить установщик ASP.NET Runtime.");

        await process.WaitForExitAsync();

        if (!AspNetRuntimeSetup.IsSuccessExitCode(process.ExitCode))
            throw new InvalidOperationException(
                $"Установка ASP.NET Runtime завершилась кодом {process.ExitCode}");

        LogInfo("Установка ASP.NET Runtime завершена.");
    }

    private async Task InstallHostAsync(string setupFolder)
    {
        var sourceHost = Path.Combine(setupFolder, HostConstants.HostExeName);
        if (!File.Exists(sourceHost))
            sourceHost = Environment.ProcessPath ?? sourceHost;

        if (!File.Exists(sourceHost))
            throw new FileNotFoundException("Не найден host exe в пакете.", HostConstants.HostExeName);

        var targetHost = Path.Combine(_installDirectory, HostConstants.HostExeName);
        if (string.Equals(Path.GetFullPath(sourceHost), Path.GetFullPath(targetHost), StringComparison.OrdinalIgnoreCase))
        {
            LogInfo("Host уже находится в каталоге установки — копирование пропущено.");
            return;
        }

        LogInfo($"Копирую host: {sourceHost} => {targetHost}");
        await CopyFileWithRetryAsync(sourceHost, targetHost);
    }

    private async Task InstallProductVersionsAsync(string setupFolder)
    {
        foreach (var productName in HostConstants.ProductNames)
        {
            var packaged = FindPackagedVersions(setupFolder, productName);
            if (packaged.Count == 0)
            {
                if (ProductProcessPolicy.IsRequiredInPackage(productName))
                    throw new InvalidOperationException(
                        $"В пакете нет продукта '{productName}'. Ожидается {productName}\\{{ver}}\\{productName}.exe.");

                LogInfo($"Продукт '{productName}' в пакете отсутствует — пропуск.");
                continue;
            }

            foreach (var (version, sourceDir) in packaged)
            {
                var productDir = Path.Combine(_installDirectory, productName);
                var versionFolder = VersionFolderAllocator.Allocate(productDir, version);
                var targetDir = Path.Combine(productDir, versionFolder);
                var partialDir = targetDir + ".partial";

                LogInfo($"Устанавливаю {productName} {versionFolder} из {sourceDir}");

                if (Directory.Exists(partialDir))
                    DeleteDirectoryWithRetry(partialDir);

                CopyProductPayload(sourceDir, partialDir, setupFolder);

                if (Directory.Exists(targetDir))
                    throw new IOException(
                        $"Каталог версии уже существует, запись в него запрещена: {targetDir}");

                Directory.Move(partialDir, targetDir);
                LogInfo($"Версия {version} продукта {productName} установлена в {targetDir}");
            }
        }

        await Task.CompletedTask;
    }

    private List<(Version Version, string SourceDir)> FindPackagedVersions(string setupFolder, string productName)
    {
        var result = new List<(Version, string)>();
        var productRoot = Path.Combine(setupFolder, productName);

        if (Directory.Exists(productRoot))
        {
            foreach (var versionDir in Directory.EnumerateDirectories(productRoot))
            {
                var name = Path.GetFileName(versionDir);
                if (!Version.TryParse(name, out var version))
                    continue;

                var exe = Path.Combine(versionDir, $"{productName}.exe");
                if (!File.Exists(exe))
                {
                    LogInfo($"Пропуск {versionDir}: нет {productName}.exe");
                    continue;
                }

                result.Add((version, versionDir));
            }
        }

        if (result.Count > 0)
            return result;

        var appDir = Path.Combine(setupFolder, "app", productName);
        var appExe = Path.Combine(appDir, $"{productName}.exe");
        if (File.Exists(appExe))
        {
            var version = ReadFileVersion(appExe) ?? new Version(0, 0);
            result.Add((version, appDir));
            return result;
        }

        var rootExe = Path.Combine(setupFolder, $"{productName}.exe");
        if (File.Exists(rootExe))
        {
            var version = ReadFileVersion(rootExe) ?? new Version(0, 0);
            result.Add((version, setupFolder));
        }

        return result;
    }

    /// <summary>
    /// Останавливает и удаляет старые службы и их каталоги.
    /// </summary>
    private void RemoveLegacyInstall()
    {
        RemoveServiceByName(HostConstants.LegacyApiServiceName);
        RemoveServiceByName(HostConstants.LegacyWebServiceName);

        KillResidualProcesses(HostConstants.LegacyApiProcessName);
        KillResidualProcesses(HostConstants.LegacyWebProcessName);

        var automationRoot = Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
            "Program Files",
            HostConstants.Manufacture);

        DeleteDirectoryWithRetry(Path.Combine(automationRoot, HostConstants.LegacyApiFolderName));
        DeleteDirectoryWithRetry(Path.Combine(automationRoot, HostConstants.LegacyWebFolderName));
    }

    private void RemoveServiceByName(string serviceName)
    {
        using var service = ServiceController.GetServices()
            .FirstOrDefault(s => string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

        if (service is null)
        {
            LogInfo($"Устаревшая служба {serviceName} не найдена.");
            return;
        }

        if (service.Status is not ServiceControllerStatus.Stopped and not ServiceControllerStatus.StopPending)
        {
            LogInfo($"Остановка устаревшей службы {serviceName}...");
            RunCmdAllowFailure($"sc stop {serviceName}");
        }

        RunCmdAllowFailure($"sc delete {serviceName}");
        RunCmdAllowFailure($"netsh advfirewall firewall delete rule name = \"{serviceName}\"");
        LogInfo($"Устаревшая служба {serviceName} удалена.");
    }

    private void EnsureServiceRegistered()
    {
        var bin = Path.Combine(_installDirectory, HostConstants.HostExeName);
        if (!File.Exists(bin))
            throw new FileNotFoundException("Host exe не найден в каталоге установки.", bin);

        using var existing = GetService();
        if (existing is not null)
        {
            LogInfo("Служба уже зарегистрирована — обновляю binPath.");
            RunCmd($"sc config {HostConstants.ServiceName} binPath= \"{bin} --service\"");
        }
        else
        {
            LogInfo("Регистрирую Windows-службу.");
            RunCmd($"sc create {HostConstants.ServiceName} binPath= \"{bin} --service\" DisplayName= \"{HostConstants.ServiceDisplayName}\" type= own start= auto");
            RunCmd($"sc failure \"{HostConstants.ServiceName}\" reset= 5 actions= restart/5000");
            RunCmdAllowFailure($"netsh advfirewall firewall delete rule name = \"{HostConstants.ServiceName}\"");
            RunCmd($"netsh advfirewall firewall add rule name = \"{HostConstants.ServiceName}\" dir =in action = allow protocol = TCP localport = {HostConstants.ApiHttpPort}");
        }

        EnsureServiceGuardTask();
    }

    /// <summary>
    /// Создаёт задачу fc-guard от SYSTEM, чтобы консоль не всплывала на рабочем столе.
    /// </summary>
    private void EnsureServiceGuardTask()
    {
        RemoveServiceGuardTask();

        var inner = $"sc query {HostConstants.ServiceName} | find \"RUNNING\" >nul || sc start {HostConstants.ServiceName}";
        LogInfo($"Создаю задачу планировщика {HostConstants.GuardTaskName}.");
        RunSchtasks(
            "/Create",
            "/TN", HostConstants.GuardTaskName,
            "/TR", $"cmd.exe /c {inner}",
            "/SC", "MINUTE",
            "/MO", "5",
            "/F",
            "/RL", "HIGHEST",
            "/RU", "SYSTEM",
            "/NP");
    }

    /// <summary>
    /// Удаляет задачу fc-guard, если она есть.
    /// </summary>
    private void RemoveServiceGuardTask()
    {
        RunCmdAllowFailure($"schtasks /Delete /TN \"{HostConstants.GuardTaskName}\" /F");
    }

    /// <summary>
    /// Пытается запустить службу; сбой не прерывает установку — повтор сделает fc-guard.
    /// </summary>
    private void StartService()
    {
        try
        {
            using var service = GetService();
            if (service is null)
            {
                LogError($"Служба '{HostConstants.ServiceName}' не найдена. Задача {HostConstants.GuardTaskName} повторит запуск.");
                return;
            }

            if (service.Status == ServiceControllerStatus.Running)
            {
                LogInfo("Служба уже запущена.");
                return;
            }

            LogInfo("Запуск службы...");
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1));
            LogInfo("Служба запущена.");
        }
        catch (Exception ex)
        {
            LogError($"Не удалось запустить службу сразу: {ex.Message}. Задача {HostConstants.GuardTaskName} повторит запуск.");
        }
    }

    private void StopAndKillService()
    {
        using var service = GetService();
        if (service is not null &&
            service.Status is not ServiceControllerStatus.Stopped and not ServiceControllerStatus.StopPending)
        {
            LogInfo("Остановка службы...");
            RunCmdAllowFailure($"sc stop {HostConstants.ServiceName}");
        }

        KillResidualProcesses(HostConstants.ServiceName);
        foreach (var productName in HostConstants.ProductNames)
            KillResidualProcesses(productName);
    }

    private void KillResidualProcesses(string processName)
    {
        var currentPid = Environment.ProcessId;

        foreach (var p in Process.GetProcessesByName(processName))
        {
            if (p.Id == currentPid)
            {
                p.Dispose();
                continue;
            }

            LogInfo($"Завершаю остаточный процесс {processName} PID={p.Id}");
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: false);
                p.WaitForExit(15_000);
            }
            p.Dispose();
        }
    }

    private async Task WaitForSourceProcessAsync(string[] args)
    {
        var raw = CliArgs.Value(args, "--waitForPid", "");
        if (!int.TryParse(raw, out var pid) || pid <= 0 || pid == Environment.ProcessId)
        {
            LogInfo("Ожидание --waitForPid пропущено.");
            return;
        }

        LogInfo($"Ожидаю завершения PID={pid}...");
        var process = Process.GetProcesses().FirstOrDefault(p => p.Id == pid);
        if (process is null)
        {
            LogInfo($"PID={pid} уже не существует.");
            return;
        }

        var exited = await Task.Run(() => process.WaitForExit(120_000));
        process.Dispose();

        if (!exited)
            throw new System.TimeoutException($"PID={pid} не завершился за 120с.");

        LogInfo($"PID={pid} завершён.");
    }

    private void WriteChecksum(string[] args)
    {
        var checksum = CliArgs.Value(args, "--checksum", "");
        if (string.IsNullOrWhiteSpace(checksum))
            return;

        var path = Path.Combine(HostPaths.DataFolder, "checksum.txt");
        File.WriteAllText(path, checksum);
        LogInfo($"Записан checksum: {path}");
    }

    private static ServiceController? GetService() =>
        ServiceController.GetServices().FirstOrDefault(s =>
            string.Equals(s.ServiceName, HostConstants.ServiceName, StringComparison.OrdinalIgnoreCase));

    private static string GetSetupFolder()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
            return Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;

        return AppContext.BaseDirectory;
    }

    private static Version? ReadFileVersion(string exePath)
    {
        var info = FileVersionInfo.GetVersionInfo(exePath);
        if (Version.TryParse(info.FileVersion, out var version))
            return new Version(version.Major, version.Minor);

        if (info.FileMajorPart > 0 || info.FileMinorPart > 0)
            return new Version(info.FileMajorPart, info.FileMinorPart);

        return null;
    }

    private void CopyProductPayload(string sourceDir, string targetDir, string setupFolder)
    {
        var normalizedSource = Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedSetup = Path.GetFullPath(setupFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var isPackageRoot = string.Equals(normalizedSource, normalizedSetup, StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, HostConstants.HostExeName, StringComparison.OrdinalIgnoreCase))
                continue;

            File.Copy(file, Path.Combine(targetDir, name), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (isPackageRoot &&
                HostConstants.ProductNames.Any(p => string.Equals(name, p, StringComparison.OrdinalIgnoreCase)))
                continue;

            CopyDirectoryRecursive(dir, Path.Combine(targetDir, name));
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectoryRecursive(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
    }

    private static async Task CopyFileWithRetryAsync(string source, string target, int retries = 5)
    {
        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                File.Copy(source, target, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < retries)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            catch (UnauthorizedAccessException) when (attempt < retries)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        File.Copy(source, target, overwrite: true);
    }

    private static bool DeleteFileWithRetry(string path, int retries = 5)
    {
        if (!File.Exists(path))
            return true;

        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch when (attempt < retries)
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }

        return !File.Exists(path);
    }

    private static bool DeleteDirectoryWithRetry(string path, int retries = 5)
    {
        if (!Directory.Exists(path))
            return true;

        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch when (attempt < retries)
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }

        return !Directory.Exists(path);
    }

    /// <summary>
    /// Запускает schtasks.exe с отдельными аргументами, без cmd.exe — кавычки /TR не ломаются.
    /// </summary>
    private void RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException("Не удалось запустить schtasks.exe.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"schtasks завершился с кодом {process.ExitCode}: {string.Join(' ', arguments)}. {stdout} {stderr}");

        if (!string.IsNullOrWhiteSpace(stdout))
            LogInfo(stdout.Trim());
    }

    private static void RunCmd(string command)
    {
        var (exitCode, stdout, stderr) = RunCmdCore(command);
        if (exitCode != 0)
            throw new InvalidOperationException(
                $"Команда завершилась с кодом {exitCode}: {command}. {stdout} {stderr}");
    }

    /// <summary>
    /// Запускает команду и игнорирует ненулевой код (удаление несуществующей службы/правила).
    /// </summary>
    private void RunCmdAllowFailure(string command)
    {
        var (exitCode, stdout, stderr) = RunCmdCore(command);
        if (exitCode != 0)
            LogInfo($"Команда завершилась с кодом {exitCode}: {command}. {stdout} {stderr}");
    }

    private static (int ExitCode, string Stdout, string Stderr) RunCmdCore(string command)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process is null)
            throw new InvalidOperationException($"Не удалось запустить: {command}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);

        return (process.ExitCode, stdout, stderr);
    }

    private void StartLog(string operation)
    {
        File.WriteAllText(_logFilePath, string.Empty);
        LogInfo($"Старт операции '{operation}'.");
    }

    private void LogInfo(string message) => WriteLog("INFO", message);

    private void LogError(string message) => WriteLog("ERROR", message);

    private void WriteLog(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{level}] {message}{Environment.NewLine}";
        Console.Write(line);
        File.AppendAllText(_logFilePath, line);
    }
}
