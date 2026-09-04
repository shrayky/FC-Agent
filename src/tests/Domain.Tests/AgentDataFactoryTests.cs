using Domain.Agent;
using Domain.Agent.Dto;
using Domain.Configuration.Constants;

namespace Domain.Tests;

[TestFixture]
public class AgentDataFactoryTests
{
    [Test]
    public void Current_кладёт_диски_и_размеры_gdb()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"fc-agent-gdb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var mainPath = Path.Combine(folder, "Main.gdb");
        var logPath = Path.Combine(folder, "Log.gdb");
        File.WriteAllBytes(mainPath, new byte[256]);
        File.WriteAllBytes(logPath, new byte[64]);

        var disks = new List<PhysicalDiskHealth>
        {
            new()
            {
                Letter = "C",
                Name = "SAMSUNG",
                Kind = DiskKind.Ssd,
                Size = 100,
                FreeSpace = 40,
                IsOs = true,
                LifePercent = 98
            }
        };

        try
        {
            var data = AgentDataFactory.Current(["Microsoft.AspNetCore.App/10.0.2"], mainPath, logPath, disks);

            Assert.That(data.Version, Is.EqualTo(ApplicationInformation.Version));
            Assert.That(data.MainGdbSize, Is.EqualTo(256));
            Assert.That(data.LogGdbSize, Is.EqualTo(64));
            Assert.That(data.Disks, Has.Count.EqualTo(1));
            Assert.That(data.Disks[0].Letter, Is.EqualTo("C"));
            Assert.That(data.Disks[0].Name, Is.EqualTo("SAMSUNG"));
            Assert.That(data.Disks[0].IsOs, Is.True);
            Assert.That(data.Disks[0].LifePercent, Is.EqualTo(98));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
