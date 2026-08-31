using Domain.Agent;
using Domain.Configuration.Constants;

namespace Domain.Tests;

[TestFixture]
public class AgentDataFactoryTests
{
    /// <summary>
    /// В карточку агента попадают размеры дисков и файлов gdb.
    /// </summary>
    [Test]
    public void Current_заполняет_размеры_дисков_и_gdb()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"fc-agent-gdb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var mainPath = Path.Combine(folder, "Main.gdb");
        var logPath = Path.Combine(folder, "Log.gdb");
        File.WriteAllBytes(mainPath, new byte[256]);
        File.WriteAllBytes(logPath, new byte[64]);

        try
        {
            var data = AgentDataFactory.Current(["Microsoft.AspNetCore.App/10.0.2"], mainPath, logPath);
            var expectedDisk = DriveMetricsReader.FromPath(mainPath);
            var windowsDisk = DriveMetricsReader.WindowsSystemDrive();

            Assert.That(data.Version, Is.EqualTo(ApplicationInformation.Version));
            Assert.That(data.WindowsDiskSize, Is.EqualTo(windowsDisk.TotalBytes));
            Assert.That(data.WindowsDiskFreeSpace, Is.GreaterThan(0));
            Assert.That(data.WindowsDiskFreeSpace, Is.LessThanOrEqualTo(data.WindowsDiskSize));
            Assert.That(data.DatabaseDiskSize, Is.EqualTo(expectedDisk.TotalBytes));
            Assert.That(data.DatabaseDiskFreeSpace, Is.GreaterThanOrEqualTo(0));
            Assert.That(data.DatabaseDiskFreeSpace, Is.LessThanOrEqualTo(data.DatabaseDiskSize));
            Assert.That(data.MainGdbSize, Is.EqualTo(256));
            Assert.That(data.LogGdbSize, Is.EqualTo(64));
            Assert.That(data.WindowsDiskLetter, Is.EqualTo(windowsDisk.Letter));
            Assert.That(data.WindowsDiskName, Is.EqualTo(windowsDisk.Name));
            Assert.That(data.DatabaseDiskLetter, Is.EqualTo(expectedDisk.Letter));
            Assert.That(data.DatabaseDiskName, Is.EqualTo(expectedDisk.Name));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
