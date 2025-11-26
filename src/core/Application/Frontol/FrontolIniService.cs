using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Transient)]
public class FrontolIniService : IFrontolIni
{
    public async Task<Result<string>> MainGdbPath()
    {
        if (!OperatingSystem.IsWindows())
            return Result.Failure<string>("Не поддерживается в данной ОС");
        
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var iniPath = Path.Combine(programData, "atol", "frontol6", "settings", "frontol.ini");

        if (!File.Exists(iniPath))
            return Result.Failure<string>($"Не найден frontol.ini в {iniPath}");
        
        var iniLines = await File.ReadAllLinesAsync(iniPath);

        foreach (var iniLine in iniLines)
        {
            var line = iniLine.Trim();
            
            if (!line.StartsWith("Path="))
                continue;

            var keyValue = line.Split("=");
            var dbFolder = keyValue[1];
            
            if (!Path.EndsInDirectorySeparator(dbFolder))
                dbFolder = string.Concat(dbFolder, Path.DirectorySeparatorChar);
            
            var mainGdbPath = Path.Combine(dbFolder, "Main.gdb");
            
            return Result.Success(mainGdbPath);
        }
        
        return Result.Failure<string>($"Не найден путь к main.gdb frontol.ini в {iniPath}");
    }
    
    public async Task<Result<(string mainPath, string logPath)>> FrontolDbPath()
    {
        if (!OperatingSystem.IsWindows())
            return Result.Failure<(string, string)>("Не поддерживается в данной ОС");
        
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var iniPath = Path.Combine(programData, "atol", "frontol6", "settings", "frontol.ini");

        if (!File.Exists(iniPath))
            return Result.Failure<(string, string)>($"Не найден frontol.ini в {iniPath}");
        
        var iniLines = await File.ReadAllLinesAsync(iniPath);

        var dbFolfer = string.Empty;
        var mainGdbPath = string.Empty;
        var logGdbPath = string.Empty;

        foreach (var iniLine in iniLines)
        {
            var line = iniLine.Trim();
            
            if (!line.Contains('='))
                continue;
            
            var keyValue = line.Split("=");
            var value = keyValue[1];

            if (line.StartsWith("Path="))
                dbFolfer = value;
            
            if (line.StartsWith("DB=") && !string.IsNullOrEmpty(dbFolfer))
                mainGdbPath = Path.Combine(dbFolfer, value);
            
            if (line.StartsWith("Log=") && !string.IsNullOrEmpty(dbFolfer))
                logGdbPath = Path.Combine(dbFolfer, value);
        }

        if (string.IsNullOrEmpty(mainGdbPath) || string.IsNullOrEmpty(logGdbPath))
            return Result.Failure<(string, string)>($"Не найдены пути к базам в frontol.ini по {iniPath}");
        
        return Result.Success((mainGdbPath, logGdbPath));
    }
}