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
        
        return Result.Failure<string>($"Не найден frontol.ini в {iniPath}");
    }
}