using Application.Frontol;
using CSharpFunctionalExtensions;
using Domain.Configuration.Options;
using Domain.Frontol.Interfaces;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FrontolConnectionGuardTests
{
    private Mock<IFrontolIni> _ini = null!;
    private FrontolConnectionGuard _guard = null!;

    [SetUp]
    public void SetUp()
    {
        _ini = new Mock<IFrontolIni>();
        _guard = new FrontolConnectionGuard(_ini.Object);
    }

    /// <summary>
    /// Конфиг совпадает с ini — обновлять не нужно.
    /// </summary>
    [Test]
    public async Task Check_совпадение_не_требует_обновления()
    {
        _ini.Setup(i => i.FrontolDbPath())
            .ReturnsAsync(Result.Success((@"D:\frontol\Main.gdb", @"D:\frontol\Log.gdb")));

        var result = await _guard.Check(new DatabaseConnection
        {
            DatabasePath = @"localhost:D:\frontol\Main.gdb",
            LogDatabasePath = @"localhost:D:\frontol\Log.gdb"
        });

        Assert.That(result.NeedUpdate, Is.False);
        Assert.That(result.Error, Is.Null);
    }

    /// <summary>
    /// Другая папка в конфиге — берём пути из ini и оставляем localhost.
    /// </summary>
    [Test]
    public async Task Check_расхождение_подставляет_пути_из_ini()
    {
        _ini.Setup(i => i.FrontolDbPath())
            .ReturnsAsync(Result.Success((@"D:\new\Main.gdb", @"D:\new\Log.gdb")));

        var result = await _guard.Check(new DatabaseConnection
        {
            DatabasePath = @"localhost:D:\old\Main.gdb",
            LogDatabasePath = @"localhost:D:\old\Log.gdb"
        });

        Assert.That(result.NeedUpdate, Is.True);
        Assert.That(result.MainPath, Is.EqualTo(@"localhost:D:\new\Main.gdb"));
        Assert.That(result.LogPath, Is.EqualTo(@"localhost:D:\new\Log.gdb"));
    }

    /// <summary>
    /// Пустые пути в конфиге заполняем из ini.
    /// </summary>
    [Test]
    public async Task Check_пустые_пути_заполняет_из_ini()
    {
        _ini.Setup(i => i.FrontolDbPath())
            .ReturnsAsync(Result.Success((@"D:\frontol\Main.gdb", @"D:\frontol\Log.gdb")));

        var result = await _guard.Check(new DatabaseConnection());

        Assert.That(result.NeedUpdate, Is.True);
        Assert.That(result.MainPath, Is.EqualTo(@"D:\frontol\Main.gdb"));
        Assert.That(result.LogPath, Is.EqualTo(@"D:\frontol\Log.gdb"));
    }

    /// <summary>
    /// Path= в ini уже с localhost — не добавляем второй и не обновляем, если путь тот же.
    /// </summary>
    [Test]
    public async Task Check_ini_с_localhost_не_дублирует_префикс()
    {
        _ini.Setup(i => i.FrontolDbPath())
            .ReturnsAsync(Result.Success((@"localhost:D:\frontol\Main.gdb", @"localhost:D:\frontol\Log.gdb")));

        var result = await _guard.Check(new DatabaseConnection
        {
            DatabasePath = @"localhost:D:\frontol\Main.gdb",
            LogDatabasePath = @"localhost:D:\frontol\Log.gdb"
        });

        Assert.That(result.NeedUpdate, Is.False);
    }

    /// <summary>
    /// Уже записанный localhost:localhost: исправляем до одного префикса.
    /// </summary>
    [Test]
    public async Task Check_двойной_localhost_выравнивает()
    {
        _ini.Setup(i => i.FrontolDbPath())
            .ReturnsAsync(Result.Success((@"localhost:D:\frontol\Main.gdb", @"localhost:D:\frontol\Log.gdb")));

        var result = await _guard.Check(new DatabaseConnection
        {
            DatabasePath = @"localhost:localhost:D:\frontol\Main.gdb",
            LogDatabasePath = @"localhost:localhost:D:\frontol\Log.gdb"
        });

        Assert.That(result.NeedUpdate, Is.True);
        Assert.That(result.MainPath, Is.EqualTo(@"localhost:D:\frontol\Main.gdb"));
        Assert.That(result.LogPath, Is.EqualTo(@"localhost:D:\frontol\Log.gdb"));
    }

    /// <summary>
    /// Нет ini — не трогаем конфиг, отдаём ошибку.
    /// </summary>
    [Test]
    public async Task Check_нет_ini_возвращает_ошибку()
    {
        _ini.Setup(i => i.FrontolDbPath())
            .ReturnsAsync(Result.Failure<(string, string)>("нет файла"));

        var result = await _guard.Check(new DatabaseConnection
        {
            DatabasePath = @"localhost:D:\frontol\Main.gdb"
        });

        Assert.That(result.NeedUpdate, Is.False);
        Assert.That(result.Error, Is.EqualTo("нет файла"));
    }
}
