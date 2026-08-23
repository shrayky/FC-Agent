using HostApp.Models;
using HostApp.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HostApp.Tests;

[TestFixture]
public class VersionCleanupTests
{
    /// <summary>
    /// Cleanup оставляет две последние версии и удаляет более старые каталоги.
    /// </summary>
    [Test]
    public void Cleanup_удаляет_версии_старше_двух_последних()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-cleanup-" + Guid.NewGuid());
        try
        {
            var v110 = CreateProductExe(root, "fc-agent", "1.10");
            var v111 = CreateProductExe(root, "fc-agent", "1.11");
            var v112 = CreateProductExe(root, "fc-agent", "1.12");
            var product = BuildProduct(root, "fc-agent", v110, v111, v112);

            var cleanup = new VersionCleanup(NullLogger<VersionCleanup>.Instance);
            cleanup.Cleanup(product, new HashSet<string>(), versionsToKeep: 2);

            Assert.That(Directory.Exists(v110), Is.False);
            Assert.That(Directory.Exists(v111), Is.True);
            Assert.That(Directory.Exists(v112), Is.True);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Каталог из protected set не удаляется, даже если он старше VersionsToKeep.
    /// </summary>
    [Test]
    public void Cleanup_не_удаляет_защищённый_каталог()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-cleanup-" + Guid.NewGuid());
        try
        {
            var v110 = CreateProductExe(root, "fc-agent", "1.10");
            var v111 = CreateProductExe(root, "fc-agent", "1.11");
            var v112 = CreateProductExe(root, "fc-agent", "1.12");
            var product = BuildProduct(root, "fc-agent", v110, v111, v112);

            var cleanup = new VersionCleanup(NullLogger<VersionCleanup>.Instance);
            cleanup.Cleanup(product, new HashSet<string> { v110 }, versionsToKeep: 2);

            Assert.That(Directory.Exists(v110), Is.True);
            Assert.That(Directory.Exists(v111), Is.True);
            Assert.That(Directory.Exists(v112), Is.True);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Создаёт раскладку {root}/{product}/{version}/{product}.exe и возвращает путь версии.
    /// </summary>
    private static string CreateProductExe(string root, string product, string version)
    {
        var versionDir = Path.Combine(root, product, version);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, product + ".exe"), "stub");
        return versionDir;
    }

    /// <summary>
    /// Собирает ProductInfo по уже созданным каталогам версий.
    /// </summary>
    private static ProductInfo BuildProduct(string root, string product, params string[] versionDirs)
    {
        var versions = versionDirs
            .Select(dir =>
            {
                var name = Path.GetFileName(dir)!;
                var version = Version.Parse(name);
                var exe = Path.Combine(dir, product + ".exe");
                return new ProductVersionInfo(version, dir, exe);
            })
            .ToList();

        return new ProductInfo(product, Path.Combine(root, product), versions);
    }

    /// <summary>
    /// Удаляет временный каталог, если он был создан.
    /// </summary>
    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
}
