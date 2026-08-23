using HostApp.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HostApp.Tests;

[TestFixture]
public class ProductDiscoveryTests
{
    /// <summary>
    /// Если есть только fc-agent, Discover возвращает один продукт без fc-remote.
    /// </summary>
    [Test]
    public void Discover_без_fc_remote_возвращает_только_агент()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-host-" + Guid.NewGuid());
        try
        {
            CreateProductExe(root, "fc-agent", "1.12");

            var discovery = new ProductDiscovery(NullLogger<ProductDiscovery>.Instance);
            var products = discovery.Discover(root);

            Assert.That(products, Has.Count.EqualTo(1));
            Assert.That(products[0].Name, Is.EqualTo("fc-agent"));
            Assert.That(products[0].Latest!.Version, Is.EqualTo(new Version(1, 12)));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Каталоги fc-agent и fc-remote оба попадают в результат Discover.
    /// </summary>
    [Test]
    public void Discover_с_агентом_и_remote_возвращает_оба()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-host-" + Guid.NewGuid());
        try
        {
            CreateProductExe(root, "fc-agent", "1.12");
            CreateProductExe(root, "fc-remote", "1.0");

            var discovery = new ProductDiscovery(NullLogger<ProductDiscovery>.Instance);
            var products = discovery.Discover(root);

            Assert.That(products, Has.Count.EqualTo(2));
            Assert.That(products.Select(p => p.Name), Is.EquivalentTo(new[] { "fc-agent", "fc-remote" }));

            var agent = products.Single(p => p.Name == "fc-agent");
            Assert.That(agent.Latest!.Version, Is.EqualTo(new Version(1, 12)));

            var remote = products.Single(p => p.Name == "fc-remote");
            Assert.That(remote.Latest!.Version, Is.EqualTo(new Version(1, 0)));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Latest указывает на старшую папку версии, а не на первую найденную.
    /// </summary>
    [Test]
    public void Discover_берёт_старшую_версию()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-host-" + Guid.NewGuid());
        try
        {
            CreateProductExe(root, "fc-agent", "1.11");
            CreateProductExe(root, "fc-agent", "1.12");

            var discovery = new ProductDiscovery(NullLogger<ProductDiscovery>.Instance);
            var products = discovery.Discover(root);

            Assert.That(products, Has.Count.EqualTo(1));
            Assert.That(products[0].Name, Is.EqualTo("fc-agent"));
            Assert.That(products[0].Latest!.Version, Is.EqualTo(new Version(1, 12)));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Трёхчастная папка 2.1.1 старше двухчастной 2.1 — новая установка должна попадать в 2.1.n.
    /// </summary>
    [Test]
    public void Discover_2_1_1_старше_2_1()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-host-" + Guid.NewGuid());
        try
        {
            CreateProductExe(root, "fc-agent", "2.1");
            CreateProductExe(root, "fc-agent", "2.1.1");

            var discovery = new ProductDiscovery(NullLogger<ProductDiscovery>.Instance);
            var products = discovery.Discover(root);

            Assert.That(products, Has.Count.EqualTo(1));
            Assert.That(products[0].Latest!.Version, Is.EqualTo(new Version(2, 1, 1)));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Пустой корень установки не даёт продуктов.
    /// </summary>
    [Test]
    public void Discover_пустой_каталог_возвращает_пустой_список()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-host-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(root);

            var discovery = new ProductDiscovery(NullLogger<ProductDiscovery>.Instance);
            var products = discovery.Discover(root);

            Assert.That(products, Is.Empty);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Папка версии без ожидаемого exe не участвует в выборе Latest.
    /// </summary>
    [Test]
    public void Discover_пропускает_версию_без_exe()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-host-" + Guid.NewGuid());
        try
        {
            CreateProductExe(root, "fc-agent", "1.11");
            Directory.CreateDirectory(Path.Combine(root, "fc-agent", "1.12"));

            var discovery = new ProductDiscovery(NullLogger<ProductDiscovery>.Instance);
            var products = discovery.Discover(root);

            Assert.That(products, Has.Count.EqualTo(1));
            Assert.That(products[0].Name, Is.EqualTo("fc-agent"));
            Assert.That(products[0].Latest!.Version, Is.EqualTo(new Version(1, 11)));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// Создаёт раскладку {root}/{product}/{version}/{product}.exe.
    /// </summary>
    private static void CreateProductExe(string root, string product, string version)
    {
        var versionDir = Path.Combine(root, product, version);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, product + ".exe"), "stub");
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
