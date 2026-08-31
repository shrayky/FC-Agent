using Domain.Agent;

namespace Domain.Tests;

[TestFixture]
public class DriveMetricsReaderTests
{
    /// <summary>
    /// Размер существующего файла в байтах.
    /// </summary>
    [Test]
    public void FileSize_возвращает_длину_файла()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fc-agent-size-{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(path, new byte[128]);

        try
        {
            Assert.That(DriveMetricsReader.FileSize(path), Is.EqualTo(128));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Нет файла — размер 0, без исключения.
    /// </summary>
    [Test]
    public void FileSize_0_если_файла_нет()
    {
        Assert.That(DriveMetricsReader.FileSize(@"C:\fc-agent-missing-file.gdb"), Is.EqualTo(0));
    }

    /// <summary>
    /// Пустой путь — размер 0.
    /// </summary>
    [Test]
    public void FileSize_0_если_путь_пустой()
    {
        Assert.That(DriveMetricsReader.FileSize(string.Empty), Is.EqualTo(0));
    }

    /// <summary>
    /// Размер gdb по пути Firebird server:C:\...
    /// </summary>
    [Test]
    public void FileSize_читает_файл_по_пути_firebird()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fc-agent-fb-{Guid.NewGuid():N}.gdb");
        File.WriteAllBytes(path, new byte[32]);

        try
        {
            Assert.That(DriveMetricsReader.FileSize($"localhost:{path}"), Is.EqualTo(32));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Путь Firebird server:C:\... сводится к локальному файлу.
    /// </summary>
    [Test]
    public void LocalFilePath_убирает_префикс_сервера()
    {
        Assert.That(
            DriveMetricsReader.LocalFilePath(@"localhost:D:\frontol\Main.gdb"),
            Is.EqualTo(@"D:\frontol\Main.gdb"));
    }

    /// <summary>
    /// Обычный путь Windows не меняется.
    /// </summary>
    [Test]
    public void LocalFilePath_оставляет_локальный_путь()
    {
        Assert.That(
            DriveMetricsReader.LocalFilePath(@"C:\frontol\Main.gdb"),
            Is.EqualTo(@"C:\frontol\Main.gdb"));
    }

    /// <summary>
    /// Пустой путь — нулевые метрики диска.
    /// </summary>
    [Test]
    public void FromPath_нули_если_путь_пустой()
    {
        var metrics = DriveMetricsReader.FromPath(string.Empty);

        Assert.That(metrics.TotalBytes, Is.EqualTo(0));
        Assert.That(metrics.FreeBytes, Is.EqualTo(0));
        Assert.That(metrics.Letter, Is.EqualTo(string.Empty));
        Assert.That(metrics.Name, Is.EqualTo(string.Empty));
    }

    /// <summary>
    /// Для temp-пути есть ненулевой размер диска.
    /// </summary>
    [Test]
    public void FromPath_читает_диск_по_существующему_пути()
    {
        var metrics = DriveMetricsReader.FromPath(Path.GetTempPath());

        Assert.That(metrics.TotalBytes, Is.GreaterThan(0));
        Assert.That(metrics.FreeBytes, Is.GreaterThanOrEqualTo(0));
        Assert.That(metrics.FreeBytes, Is.LessThanOrEqualTo(metrics.TotalBytes));
        Assert.That(metrics.Letter, Is.EqualTo(Path.GetPathRoot(Path.GetTempPath())![0].ToString()));
        Assert.That(metrics.Name, Is.EqualTo(new DriveInfo(metrics.Letter).VolumeLabel));
    }
}
