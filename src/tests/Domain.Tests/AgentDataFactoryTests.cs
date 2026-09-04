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
                Name = "SAMSUNG",
                Kind = DiskKind.Ssd,
                LifePercent = 98,
                Partitions =
                [
                    new DiskPartition
                    {
                        Letter = "C",
                        Name = "System",
                        Size = 100,
                        FreeSpace = 40,
                        IsOs = true
                    }
                ]
            }
        };

        try
        {
            var data = AgentDataFactory.Current(["Microsoft.AspNetCore.App/10.0.2"], mainPath, logPath, disks);

            Assert.That(data.Version, Is.EqualTo(ApplicationInformation.Version));
            Assert.That(data.MainGdbSize, Is.EqualTo(256));
            Assert.That(data.LogGdbSize, Is.EqualTo(64));
            Assert.That(data.Disks, Has.Count.EqualTo(1));
            Assert.That(data.Disks[0].Name, Is.EqualTo("SAMSUNG"));
            Assert.That(data.Disks[0].LifePercent, Is.EqualTo(98));
            Assert.That(data.Disks[0].Partitions, Has.Count.EqualTo(1));
            Assert.That(data.Disks[0].Partitions[0].Letter, Is.EqualTo("C"));
            Assert.That(data.Disks[0].Partitions[0].IsOs, Is.True);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
