using Domain.Frontol;

namespace Domain.Tests;

[TestFixture]
public class FrontolDbPathComparerTests
{
    /// <summary>
    /// localhost:C:\... и тот же файл без префикса — одна база.
    /// </summary>
    [Test]
    public void Same_true_если_отличается_только_префикс_сервера()
    {
        Assert.That(
            FrontolDbPathComparer.Same(@"localhost:D:\frontol\Main.gdb", @"D:\frontol\Main.gdb"),
            Is.True);
    }

    /// <summary>
    /// Разный регистр имени файла не считается другой базой.
    /// </summary>
    [Test]
    public void Same_true_если_разный_регистр()
    {
        Assert.That(
            FrontolDbPathComparer.Same(@"D:\Frontol\MAIN.GDB", @"d:\frontol\main.gdb"),
            Is.True);
    }

    /// <summary>
    /// Другая папка — другая база.
    /// </summary>
    [Test]
    public void Same_false_если_другая_папка()
    {
        Assert.That(
            FrontolDbPathComparer.Same(@"localhost:D:\old\Main.gdb", @"D:\new\Main.gdb"),
            Is.False);
    }

    /// <summary>
    /// Пустой путь в конфиге не совпадает с ini.
    /// </summary>
    [Test]
    public void Same_false_если_путь_в_конфиге_пустой()
    {
        Assert.That(FrontolDbPathComparer.Same(string.Empty, @"D:\frontol\Main.gdb"), Is.False);
    }

    /// <summary>
    /// Сохраняем localhost: при подстановке пути из ini.
    /// </summary>
    [Test]
    public void WithConfiguredServer_оставляет_префикс_сервера()
    {
        Assert.That(
            FrontolDbPathComparer.WithConfiguredServer(@"localhost:D:\old\Main.gdb", @"D:\new\Main.gdb"),
            Is.EqualTo(@"localhost:D:\new\Main.gdb"));
    }

    /// <summary>
    /// Без префикса сервера пишем путь из ini как есть.
    /// </summary>
    [Test]
    public void WithConfiguredServer_без_префикса_пишет_путь_ini()
    {
        Assert.That(
            FrontolDbPathComparer.WithConfiguredServer(@"D:\old\Main.gdb", @"D:\new\Main.gdb"),
            Is.EqualTo(@"D:\new\Main.gdb"));
    }

    /// <summary>
    /// Пустой конфиг: сервер берём из ini, если он там есть.
    /// </summary>
    [Test]
    public void WithConfiguredServer_пустой_конфиг_берёт_сервер_из_ini()
    {
        Assert.That(
            FrontolDbPathComparer.WithConfiguredServer(string.Empty, @"localhost:D:\new\Main.gdb"),
            Is.EqualTo(@"localhost:D:\new\Main.gdb"));
    }

    /// <summary>
    /// Path= в ini уже с localhost — не клеим второй.
    /// </summary>
    [Test]
    public void WithConfiguredServer_не_дублирует_localhost_из_ini()
    {
        Assert.That(
            FrontolDbPathComparer.WithConfiguredServer(
                @"localhost:D:\old\Main.gdb",
                @"localhost:D:\new\Main.gdb"),
            Is.EqualTo(@"localhost:D:\new\Main.gdb"));
    }

    /// <summary>
    /// Уже записанный localhost:localhost: сводим к одному префиксу.
    /// </summary>
    [Test]
    public void WithConfiguredServer_убирает_двойной_localhost()
    {
        Assert.That(
            FrontolDbPathComparer.WithConfiguredServer(
                @"localhost:localhost:D:\old\Main.gdb",
                @"localhost:D:\new\Main.gdb"),
            Is.EqualTo(@"localhost:D:\new\Main.gdb"));
    }

    /// <summary>
    /// ini с localhost и конфиг с localhost — одна база.
    /// </summary>
    [Test]
    public void Same_true_если_localhost_в_обоих()
    {
        Assert.That(
            FrontolDbPathComparer.Same(
                @"localhost:D:\frontol\Main.gdb",
                @"localhost:D:\frontol\Main.gdb"),
            Is.True);
    }

    /// <summary>
    /// Двойной localhost в конфиге — всё ещё та же база.
    /// </summary>
    [Test]
    public void Same_true_если_в_конфиге_двойной_localhost()
    {
        Assert.That(
            FrontolDbPathComparer.Same(
                @"localhost:localhost:D:\frontol\Main.gdb",
                @"localhost:D:\frontol\Main.gdb"),
            Is.True);
    }
}
