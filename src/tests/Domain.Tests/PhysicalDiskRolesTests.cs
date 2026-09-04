using Domain.Agent;
using Domain.Agent.Dto;

namespace Domain.Tests;

[TestFixture]
public class PhysicalDiskRolesTests
{
    [Test]
    public void Apply_ставит_флаги_на_каждый_раздел()
    {
        var partitions = new List<DiskPartition>
        {
            new() { Letter = "c", Name = "System" },
            new() { Letter = "d", Name = "DATA" }
        };

        PhysicalDiskRoles.Apply(partitions, osLetter: "C", dbLetter: "D");

        Assert.That(partitions[0].Letter, Is.EqualTo("C"));
        Assert.That(partitions[0].IsOs, Is.True);
        Assert.That(partitions[0].IsDatabase, Is.False);
        Assert.That(partitions[1].Letter, Is.EqualTo("D"));
        Assert.That(partitions[1].IsOs, Is.False);
        Assert.That(partitions[1].IsDatabase, Is.True);
    }

    [Test]
    public void Apply_бд_и_ос_на_одном_разделе()
    {
        var partitions = new List<DiskPartition> { new() { Letter = "C" } };

        PhysicalDiskRoles.Apply(partitions, osLetter: "C", dbLetter: "C");

        Assert.That(partitions[0].IsOs, Is.True);
        Assert.That(partitions[0].IsDatabase, Is.True);
    }

    [Test]
    public void Apply_пустая_буква_бд_не_ставит_флаг()
    {
        var partitions = new List<DiskPartition> { new() { Letter = "C" } };

        PhysicalDiskRoles.Apply(partitions, osLetter: "C", dbLetter: "");

        Assert.That(partitions[0].IsOs, Is.True);
        Assert.That(partitions[0].IsDatabase, Is.False);
    }
}
