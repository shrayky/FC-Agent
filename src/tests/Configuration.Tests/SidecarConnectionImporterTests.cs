using Configuration.Services;
using Domain.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Configuration.Tests;

[TestFixture]
public class SidecarConnectionImporterTests
{
    private string _tempDir = null!;
    private SidecarConnectionImporter _importer = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fc-sidecar-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _importer = new SidecarConnectionImporter(NullLogger<SidecarConnectionImporter>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Пустые Address и Token заполняются из config.json рядом с exe.
    /// </summary>
    [Test]
    public void TryImport_заполняет_пустые_настройки_из_файла()
    {
        WriteSidecar("""
            {
              "Address": "http://localhost:2590",
              "Token": "e880fc66-93f7-43e4-b48a-3ad98016e99a"
            }
            """);

        var settings = new Parameters();

        var imported = _importer.TryImport(settings, _tempDir);

        Assert.That(imported, Is.True);
        Assert.That(settings.CentralServerSettings.Address, Is.EqualTo("http://localhost:2590"));
        Assert.That(settings.CentralServerSettings.Token, Is.EqualTo("e880fc66-93f7-43e4-b48a-3ad98016e99a"));
    }

    /// <summary>
    /// Уже заданные Address и Token не перезаписываются sidecar-файлом.
    /// </summary>
    [Test]
    public void TryImport_не_перезаписывает_заполненные_настройки()
    {
        WriteSidecar("""
            {
              "Address": "http://localhost:2590",
              "Token": "e880fc66-93f7-43e4-b48a-3ad98016e99a"
            }
            """);

        var settings = new Parameters();
        settings.CentralServerSettings.Address = "http://already-set";
        settings.CentralServerSettings.Token = "already-set-token";

        var imported = _importer.TryImport(settings, _tempDir);

        Assert.That(imported, Is.False);
        Assert.That(settings.CentralServerSettings.Address, Is.EqualTo("http://already-set"));
        Assert.That(settings.CentralServerSettings.Token, Is.EqualTo("already-set-token"));
    }

    /// <summary>
    /// Если заполнен только Address, sidecar не применяется.
    /// </summary>
    [Test]
    public void TryImport_пропускает_если_заполнен_только_адрес()
    {
        WriteSidecar("""
            {
              "Address": "http://localhost:2590",
              "Token": "e880fc66-93f7-43e4-b48a-3ad98016e99a"
            }
            """);

        var settings = new Parameters();
        settings.CentralServerSettings.Address = "http://already-set";

        var imported = _importer.TryImport(settings, _tempDir);

        Assert.That(imported, Is.False);
        Assert.That(settings.CentralServerSettings.Token, Is.Empty);
    }

    /// <summary>
    /// Нет config.json — настройки не меняются.
    /// </summary>
    [Test]
    public void TryImport_пропускает_если_файла_нет()
    {
        var settings = new Parameters();

        var imported = _importer.TryImport(settings, _tempDir);

        Assert.That(imported, Is.False);
        Assert.That(settings.CentralServerSettings.Address, Is.Empty);
        Assert.That(settings.CentralServerSettings.Token, Is.Empty);
    }

    /// <summary>
    /// Битый JSON не роняет агент и не меняет настройки.
    /// </summary>
    [Test]
    public void TryImport_пропускает_битый_json()
    {
        WriteSidecar("{ not-json");

        var settings = new Parameters();

        var imported = _importer.TryImport(settings, _tempDir);

        Assert.That(imported, Is.False);
        Assert.That(settings.CentralServerSettings.Address, Is.Empty);
        Assert.That(settings.CentralServerSettings.Token, Is.Empty);
    }

    /// <summary>
    /// Пустые Address/Token в sidecar не записываются в настройки.
    /// </summary>
    [Test]
    public void TryImport_пропускает_пустые_значения_в_файле()
    {
        WriteSidecar("""
            {
              "Address": "",
              "Token": ""
            }
            """);

        var settings = new Parameters();

        var imported = _importer.TryImport(settings, _tempDir);

        Assert.That(imported, Is.False);
    }

    /// <summary>
    /// После импорта sidecar-файл остаётся на диске.
    /// </summary>
    [Test]
    public void TryImport_не_удаляет_файл()
    {
        WriteSidecar("""
            {
              "Address": "http://localhost:2590",
              "Token": "e880fc66-93f7-43e4-b48a-3ad98016e99a"
            }
            """);

        _importer.TryImport(new Parameters(), _tempDir);

        Assert.That(File.Exists(SidecarPath()), Is.True);
    }

    private void WriteSidecar(string json)
    {
        File.WriteAllText(SidecarPath(), json);
    }

    private string SidecarPath() => Path.Combine(_tempDir, "config.json");
}
