using System.Diagnostics;
using Domain.Agent;

namespace Domain.Tests;

[TestFixture]
public class DriverAto10lLogsSizeReaderTests
{
    private string _root = string.Empty;
    private readonly List<string> _junctions = [];

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"fc-agent-atol-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        // Junction удаляется отдельно: рекурсивное удаление идёт по ссылке и упирается в отказ доступа.
        foreach (var junction in _junctions.Where(Directory.Exists))
            Directory.Delete(junction);

        _junctions.Clear();

        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    /// <summary>
    /// Логи лежат в подкаталогах — размеры всех файлов складываются.
    /// </summary>
    [Test]
    public void DirectorySizeBytes_суммирует_вложенные_каталоги()
    {
        WriteFile(Path.Combine(_root, "2026-09-01", "driver.log"), 100);
        WriteFile(Path.Combine(_root, "2026-09-02", "nested", "driver.log"), 200);
        WriteFile(Path.Combine(_root, "driver.log"), 300);

        Assert.That(DriverAto10lLogsSizeReader.DirectorySizeBytes(_root), Is.EqualTo(600));
    }

    /// <summary>
    /// Каталога нет — 0, без исключения.
    /// </summary>
    [Test]
    public void DirectorySizeBytes_0_если_каталога_нет()
    {
        var missing = Path.Combine(_root, "missing");

        Assert.That(DriverAto10lLogsSizeReader.DirectorySizeBytes(missing), Is.EqualTo(0));
    }

    /// <summary>
    /// Пустой каталог — 0.
    /// </summary>
    [Test]
    public void DirectorySizeBytes_0_если_каталог_пустой()
    {
        Assert.That(DriverAto10lLogsSizeReader.DirectorySizeBytes(_root), Is.EqualTo(0));
    }

    /// <summary>
    /// Пустой путь — 0.
    /// </summary>
    [Test]
    public void DirectorySizeBytes_0_если_путь_пустой()
    {
        Assert.That(DriverAto10lLogsSizeReader.DirectorySizeBytes(string.Empty), Is.EqualTo(0));
    }

    /// <summary>
    /// Файл занят монопольно — размер берётся через FileInfo, обход не падает.
    /// </summary>
    [Test]
    public void DirectorySizeBytes_читает_занятый_файл()
    {
        var locked = Path.Combine(_root, "driver.log");
        WriteFile(locked, 100);
        WriteFile(Path.Combine(_root, "nested", "driver.log"), 200);

        var errors = new List<string>();

        using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.That(DriverAto10lLogsSizeReader.DirectorySizeBytes(_root, errors.Add), Is.EqualTo(300));
        }

        Assert.That(errors, Is.Empty);
    }

    /// <summary>
    /// Точка повторной обработки (junction) не обходится: иначе размер посчитался бы дважды.
    /// </summary>
    [Test]
    public void DirectorySizeBytes_не_идёт_по_точке_повторной_обработки()
    {
        WriteFile(Path.Combine(_root, "target", "driver.log"), 100);

        if (!TryCreateJunction(Path.Combine(_root, "link"), Path.Combine(_root, "target")))
            Assert.Ignore("Junction недоступен на этой машине");

        Assert.That(DriverAto10lLogsSizeReader.DirectorySizeBytes(_root), Is.EqualTo(100));
    }

    /// <summary>
    /// Обходятся профили всех пользователей, каталоги не-профилей (Public, Default) отбрасываются.
    /// </summary>
    [Test]
    public void List_возвращает_каталоги_логов_по_профилям()
    {
        var usersRoot = Path.Combine(_root, "Users");
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(usersRoot, "cashier2", DriverAto10lLogsDirectory.RelativePath),
            Path.Combine(usersRoot, "cashier1", DriverAto10lLogsDirectory.RelativePath),
            Path.Combine(usersRoot, "Public", DriverAto10lLogsDirectory.RelativePath)
        };

        Directory.CreateDirectory(Path.Combine(usersRoot, "cashier1"));
        Directory.CreateDirectory(Path.Combine(usersRoot, "cashier2"));
        Directory.CreateDirectory(Path.Combine(usersRoot, "Default"));
        Directory.CreateDirectory(Path.Combine(usersRoot, "Public"));

        var directories = DriverAto10lLogsDirectory.List(() => usersRoot, existing.Contains);

        Assert.That(directories, Is.EqualTo(new[]
        {
            Path.Combine(usersRoot, "cashier1", DriverAto10lLogsDirectory.RelativePath),
            Path.Combine(usersRoot, "cashier2", DriverAto10lLogsDirectory.RelativePath)
        }));
    }

    /// <summary>
    /// Ни одного каталога логов — пустой список.
    /// </summary>
    [Test]
    public void List_пустой_список_если_каталогов_логов_нет()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(Path.Combine(usersRoot, "cashier1"));

        Assert.That(DriverAto10lLogsDirectory.List(() => usersRoot, _ => false), Is.Empty);
    }

    /// <summary>
    /// Корня пользователей нет — пустой список, без исключения.
    /// </summary>
    [Test]
    public void List_пустой_список_если_корня_пользователей_нет()
    {
        var missing = Path.Combine(_root, "Users");

        Assert.That(DriverAto10lLogsDirectory.List(() => missing, _ => true), Is.Empty);
    }

    /// <summary>
    /// Каталог логов строится от профиля пользователя: AppData\Roaming\ATOL\Drivers10\Logs.
    /// </summary>
    [Test]
    public void List_собирает_путь_логов_от_профиля()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(Path.Combine(usersRoot, "cashier"));

        var directories = DriverAto10lLogsDirectory.List(() => usersRoot, _ => true);

        Assert.That(directories, Is.EqualTo(new[]
        {
            Path.Combine(usersRoot, "cashier", "AppData", "Roaming", "ATOL", "Drivers10", "Logs")
        }));
    }

    private static void WriteFile(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    // Junction создаётся без прав администратора, в отличие от символической ссылки.
    private bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
            {
                UseShellExecute = false
            });

            process?.WaitForExit();
        }
        catch (Exception)
        {
            return false;
        }

        if (!Directory.Exists(linkPath))
            return false;

        _junctions.Add(linkPath);
        return true;
    }
}
