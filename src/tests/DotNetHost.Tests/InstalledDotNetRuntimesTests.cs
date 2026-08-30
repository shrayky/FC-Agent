using DotNetHost;

namespace DotNetHost.Tests;

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

    /// <summary>
    /// Одна версия из двух разных shared-корней попадает в список один раз.
    /// </summary>
    [Test]
    public void ListFromRoots_не_дублирует_одинаковую_версию_из_двух_корней()
    {
        var rootA = Path.Combine(Path.GetTempPath(), "fc-dotnet-a-" + Guid.NewGuid());
        var rootB = Path.Combine(Path.GetTempPath(), "fc-dotnet-b-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(rootA, "Microsoft.AspNetCore.App", "10.0.11"));
        Directory.CreateDirectory(Path.Combine(rootB, "Microsoft.AspNetCore.App", "10.0.11"));

        try
        {
            var list = InstalledDotNetRuntimes.ListFromRoots([rootA, rootB]);

            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list, Does.Contain("Microsoft.AspNetCore.App/10.0.11"));
        }
        finally
        {
            Directory.Delete(rootA, true);
            Directory.Delete(rootB, true);
        }
    }

    /// <summary>
    /// Папка shared без host\fxr не считается установленным runtime — apphost его не найдёт.
    /// </summary>
    [Test]
    public void ListFromDotNetRoot_пусто_без_hostfxr()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-dotnet-root-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.AspNetCore.App", "10.0.11"));

        try
        {
            Assert.That(InstalledDotNetRuntimes.ListFromDotNetRoot(root), Is.Empty);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// При наличии host\fxr shared framework попадает в список.
    /// </summary>
    [Test]
    public void ListFromDotNetRoot_читает_shared_если_есть_fxr()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-dotnet-root-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "host", "fxr", "10.0.11"));
        Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.NETCore.App", "10.0.11"));
        Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.AspNetCore.App", "10.0.11"));

        try
        {
            var list = InstalledDotNetRuntimes.ListFromDotNetRoot(root);

            Assert.That(list, Does.Contain("Microsoft.NETCore.App/10.0.11"));
            Assert.That(list, Does.Contain("Microsoft.AspNetCore.App/10.0.11"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// Один и тот же каталог, переданный дважды, не даёт дублей.
    /// </summary>
    [Test]
    public void ListFromRoots_не_дублирует_один_и_тот_же_корень()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-dotnet-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Microsoft.AspNetCore.App", "10.0.11"));

        try
        {
            var list = InstalledDotNetRuntimes.ListFromRoots([root, root]);

            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list, Does.Contain("Microsoft.AspNetCore.App/10.0.11"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
