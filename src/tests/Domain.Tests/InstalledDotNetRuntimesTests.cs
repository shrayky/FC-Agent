using Domain.DotNet;

namespace Domain.Tests;

[TestFixture]
public class InstalledDotNetRuntimesTests
{
    /// <summary>
    /// Из shared\{name}\{version} собираются строки Имя/версия.
    /// </summary>
    [Test]
    public void ListFromRoots_читает_shared_framework()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-dotnet-" + Guid.NewGuid());
        var fx = Path.Combine(root, "Microsoft.AspNetCore.App", "10.0.2");
        Directory.CreateDirectory(fx);

        try
        {
            var list = InstalledDotNetRuntimes.ListFromRoots([root]);

            Assert.That(list, Does.Contain("Microsoft.AspNetCore.App/10.0.2"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// Каталоги без номера версии не попадают в список.
    /// </summary>
    [Test]
    public void ListFromRoots_пропускает_неверсионные_папки()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-dotnet-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Microsoft.NETCore.App", "packs"));

        try
        {
            var list = InstalledDotNetRuntimes.ListFromRoots([root]);

            Assert.That(list, Is.Empty);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
