using Configuration.Services;
using Domain.Configuration;
using Domain.Configuration.Options;

namespace Configuration.Tests;

[TestFixture]
public class StartupBindingChangedTests
{
    [Test]
    public void Одинаковые_настройки_перезапуск_не_требуют()
    {
        Assert.That(ConfigurationService.StartupBindingChanged(Sample(), Sample()), Is.False);
    }

    [Test]
    public void Путь_базы_порт_логин_пароль_и_лог_требуют_перезапуск()
    {
        AssertChanged(parameters => parameters.DatabaseConnection.DatabasePath = @"D:\other.gdb");
        AssertChanged(parameters => parameters.DatabaseConnection.LogDatabasePath = @"D:\other-log.gdb");
        AssertChanged(parameters => parameters.DatabaseConnection.UserName = "OTHER");
        AssertChanged(parameters => parameters.DatabaseConnection.Password = "other");
        AssertChanged(parameters => parameters.ServerSettings.ApiIpPort = 2590);
        AssertChanged(parameters => parameters.LoggerSettings.IsEnabled = false);
        AssertChanged(parameters => parameters.LoggerSettings.LogLevel = "Debug");
        AssertChanged(parameters => parameters.LoggerSettings.LogDepth = 7);
    }

    private static void AssertChanged(Action<Parameters> change)
    {
        var updated = Sample();
        change(updated);

        Assert.That(ConfigurationService.StartupBindingChanged(Sample(), updated), Is.True);
    }

    private static Parameters Sample() => new()
    {
        DatabaseConnection = new DatabaseConnection
        {
            DatabasePath = @"C:\frontol\main.gdb",
            LogDatabasePath = @"C:\frontol\log.gdb",
            UserName = "SYSDBA",
            Password = "masterkey"
        },
        ServerSettings = new ServerSettings { ApiIpPort = 2587 },
        LoggerSettings = new LogSettings
        {
            IsEnabled = true,
            LogLevel = "Warning",
            LogDepth = 30
        }
    };
}
