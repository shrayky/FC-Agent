using Application.Frontol;
using Domain.Configuration.Constants;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.FilesFolders;

namespace Domain.Tests;

[TestFixture]
public class FrontolScriptLibrariesServiceTests
{
    private string _root = string.Empty;
    private string _directory = string.Empty;
    private FrontolScriptLibrariesService _service = null!;

    [SetUp]
    public void SetUp()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Библиотеки скриптов фронтола читаются и пишутся только в Windows");

        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _directory = Path.Combine(_root, "fScript");
        Directory.CreateDirectory(_directory);

        _service = new FrontolScriptLibrariesService(NullLogger<FrontolScriptLibrariesService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Test]
    public void Каталог_библиотек_лежит_в_каталоге_данных_агента()
    {
        var agentDataFolder = Folders.CommonApplicationDataFolder(
            ApplicationInformation.Manufacture, ApplicationInformation.Name);

        Assert.That(FrontolScriptLibrariesService.DefaultDirectory, Is.EqualTo(Path.Combine(agentDataFolder, "fScript")));
        Assert.That(FrontolScriptLibrariesService.DefaultDirectory, Does.Not.Contain("fc-agent"));
    }

    [Test]
    public async Task ToFiles_пишет_файлы_в_UTF8_без_BOM()
    {
        const string script = "function toJson(value) { return value; }";

        var result = await _service.ToFiles([Library("json-utils.js", script)], _directory);

        Assert.That(result.IsSuccess, Is.True);

        var path = Path.Combine(_directory, "json-utils.js");
        var bytes = await File.ReadAllBytesAsync(path);

        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(script));
        Assert.That(bytes[0], Is.EqualTo((byte)'f'));
    }

    [Test]
    public async Task ToFiles_с_пустым_списком_очищает_файлы_но_не_подкаталоги()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "old.js"), "old");
        Directory.CreateDirectory(Path.Combine(_directory, "nested"));

        var result = await _service.ToFiles([], _directory);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.GetFiles(_directory), Is.Empty);
        Assert.That(Directory.Exists(Path.Combine(_directory, "nested")), Is.True);
    }

    [Test]
    public async Task ToFiles_не_пишет_за_пределы_каталога()
    {
        var result = await _service.ToFiles([Library(@"..\evil.js", "alert(1)")], _directory);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(File.Exists(Path.Combine(_root, "evil.js")), Is.False);
        Assert.That(File.Exists(Path.Combine(_directory, "evil.js")), Is.True);
    }

    [Test]
    public async Task ToFiles_заменяет_прежний_набор()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "old.js"), "old");

        var result = await _service.ToFiles([Library("new.js", "new")], _directory);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.GetFiles(_directory).Select(Path.GetFileName), Is.EqualTo(new[] { "new.js" }));
    }

    [Test]
    public async Task FromFiles_возвращает_файлы_по_алфавиту()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "b.js"), "b");
        await File.WriteAllTextAsync(Path.Combine(_directory, "a.js"), "a");
        await File.WriteAllTextAsync(Path.Combine(_directory, "c.js"), "c");
        await File.WriteAllTextAsync(Path.Combine(_directory, "skip.txt"), "skip");

        var result = await _service.FromFiles(_directory);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(library => library.FileName), Is.EqualTo(new[] { "a.js", "b.js", "c.js" }));
        Assert.That(result.Value[1].Script, Is.EqualTo("b"));
    }

    [Test]
    public async Task FromFiles_пустой_список_на_отсутствующем_каталоге()
    {
        var result = await _service.FromFiles(Path.Combine(_root, "missing"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task ToFiles_плохое_имя_или_текст_не_трогает_каталог()
    {
        var oldPath = Path.Combine(_directory, "old.js");
        await File.WriteAllTextAsync(oldPath, "old");

        var cases = new[]
        {
            new[] { Library("", "x") },
            new[] { Library("CON.js", "x") },
            new[] { Library("bad?.js", "x") },
            new[] { Library("ok.js", null!) },
            new[] { Library("A.js", "a"), Library("a.js", "b") },
            new[] { Library("good.js", "new"), Library("LPT1.js", "x") }
        };

        foreach (var libraries in cases)
        {
            var result = await _service.ToFiles(libraries, _directory);

            Assert.That(result.IsFailure, Is.True, result.IsFailure ? result.Error : "ожидался отказ");
            Assert.That(await File.ReadAllTextAsync(oldPath), Is.EqualTo("old"));
            Assert.That(File.Exists(Path.Combine(_directory, "good.js")), Is.False);
            Assert.That(Directory.Exists(_directory + ".staging"), Is.False);
        }
    }

    [Test]
    public async Task ToFiles_после_успеха_не_оставляет_staging()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "old.js"), "old");

        var result = await _service.ToFiles([Library("new.js", "new")], _directory);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(Directory.Exists(_directory + ".staging"), Is.False);
        Assert.That(await File.ReadAllTextAsync(Path.Combine(_directory, "new.js")), Is.EqualTo("new"));
    }

    [Test]
    public async Task ToFiles_отказывается_стирать_корень_диска_и_ProgramData()
    {
        var root = Path.GetPathRoot(_directory)!;
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var rootResult = await _service.ToFiles([], root);
        var programDataResult = await _service.ToFiles([], programData);

        Assert.That(rootResult.IsFailure, Is.True);
        Assert.That(programDataResult.IsFailure, Is.True);
        Assert.That(rootResult.Error, Does.Contain(root));
        Assert.That(Directory.Exists(_directory), Is.True);
    }

    private static ScriptLibrary Library(string fileName, string script) =>
        new() { FileName = fileName, Script = script };
}
