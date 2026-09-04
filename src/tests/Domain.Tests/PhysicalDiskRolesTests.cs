using Domain.Agent;
using Domain.Agent.Dto;

namespace Domain.Tests;

[TestFixture]
public class PhysicalDiskRolesTests
{
    [Test]
    public void Apply_ставит_флаги_по_буквам_томов()
    {
        var disk = new PhysicalDiskHealth { Letter = "C" };

        PhysicalDiskRoles.Apply(disk, ["C", "D"], osLetter: "C", dbLetter: "D");

        Assert.That(disk.IsOs, Is.True);
        Assert.That(disk.IsDatabase, Is.True);
        Assert.That(disk.Letter, Is.EqualTo("C"));
    }

    [Test]
    public void Apply_буква_ос_важнее_остальных()
    {
        var disk = new PhysicalDiskHealth();

        PhysicalDiskRoles.Apply(disk, ["D", "C"], osLetter: "C", dbLetter: "E");

        Assert.That(disk.Letter, Is.EqualTo("C"));
        Assert.That(disk.IsOs, Is.True);
        Assert.That(disk.IsDatabase, Is.False);
    }

    [Test]
    public void Apply_буква_бд_если_ос_на_другом_диске()
    {
        var disk = new PhysicalDiskHealth();

        PhysicalDiskRoles.Apply(disk, ["E", "D"], osLetter: "C", dbLetter: "D");

        Assert.That(disk.Letter, Is.EqualTo("D"));
        Assert.That(disk.IsOs, Is.False);
        Assert.That(disk.IsDatabase, Is.True);
    }
}
