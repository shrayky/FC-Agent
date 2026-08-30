using Domain.Agent;

namespace Domain.Tests;

[TestFixture]
public class UpdateDiskGuardTests
{
    /// <summary>
    /// Меньше 500 МБ — скачивать обновление нельзя.
    /// </summary>
    [Test]
    public void HasEnoughSpace_false_если_меньше_500_мб()
    {
        Assert.That(UpdateDiskGuard.HasEnoughSpace(UpdateDiskGuard.MinFreeBytes - 1), Is.False);
    }

    /// <summary>
    /// Ровно 500 МБ — скачивать можно.
    /// </summary>
    [Test]
    public void HasEnoughSpace_true_если_ровно_500_мб()
    {
        Assert.That(UpdateDiskGuard.HasEnoughSpace(UpdateDiskGuard.MinFreeBytes), Is.True);
    }

    /// <summary>
    /// Порог — 500 мегабайт.
    /// </summary>
    [Test]
    public void MinFreeBytes_это_500_мб()
    {
        Assert.That(UpdateDiskGuard.MinFreeBytes, Is.EqualTo(500L * 1024 * 1024));
    }
}
