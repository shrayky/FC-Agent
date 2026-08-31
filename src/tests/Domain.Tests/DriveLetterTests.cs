using Domain.Agent;

namespace Domain.Tests;

[TestFixture]
public class DriveLetterTests
{
    /// <summary>
    /// Корень C:\ — буква C.
    /// </summary>
    [Test]
    public void DriveLetter_из_корня_это_одна_буква()
    {
        Assert.That(DriveMetricsReader.DriveLetter(@"C:\"), Is.EqualTo("C"));
    }

    /// <summary>
    /// Пустой корень — пустая буква.
    /// </summary>
    [Test]
    public void DriveLetter_пустой_если_корня_нет()
    {
        Assert.That(DriveMetricsReader.DriveLetter(string.Empty), Is.EqualTo(string.Empty));
    }
}
