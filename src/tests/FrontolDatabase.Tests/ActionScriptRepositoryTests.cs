using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

/// <summary>
/// Стартовый скрипт Frontol хранится в двух местах: код — в настройке SETTINGS.StartScript,
/// текст — в ACTSCRIPT. В старой базе строки StartScript может не быть вовсе.
/// </summary>
[TestFixture]
public class ActionScriptRepositoryTests
{
    private MainDbCtx _ctx = null!;
    private Mock<IFrontolMainDb> _mainDb = null!;
    private ActionScriptRepository _repository = null!;
    private int _nextId = 9000;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _ctx = new MainDbCtx(options);
        _ctx.Settings = _ctx.Set<Settings>();
        _ctx.ActionScripts = _ctx.Set<ActScript>();

        _nextId = 9000;
        _mainDb = new Mock<IFrontolMainDb>();
        _mainDb.Setup(m => m.NextChangeId()).Returns(() => Task.FromResult(_nextId++));

        var settings = new SettingsRepository(
            new Mock<ILogger<SettingsRepository>>().Object,
            _ctx,
            _mainDb.Object);

        _repository = new ActionScriptRepository(
            new Mock<ILogger<ActionScriptRepository>>().Object,
            _ctx,
            _mainDb.Object,
            settings);
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    [Test]
    public async Task FromDb_без_строки_StartScript_отдаёт_пустой_скрипт()
    {
        var result = await _repository.FromDb();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);
        Assert.That(result.Value.Text, Is.Empty);
    }

    [Test]
    public async Task FromDb_с_нечисловым_значением_не_падает()
    {
        await SeedSetting("StartScript", "не число");

        var result = await _repository.FromDb();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);
        Assert.That(result.Value.Text, Is.Empty);
    }

    [Test]
    public async Task FromDb_читает_скрипт_по_коду()
    {
        await SeedSetting("StartScript", "7");
        await SeedScript(7, "// script");

        var result = await _repository.FromDb();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);
        Assert.That(result.Value.Code, Is.EqualTo(7));
        Assert.That(result.Value.Text, Is.EqualTo("// script"));
    }

    [Test]
    public async Task ToDb_создаёт_отсутствующую_настройку()
    {
        var result = await _repository.ToDb(new ActionScript { Code = 5, Name = "main", Text = "// new" });

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);

        var setting = await _ctx.Settings!.AsNoTracking().SingleOrDefaultAsync(s => s.Name == "StartScript");
        var script = await _ctx.ActionScripts!.AsNoTracking().SingleAsync(a => a.Code == 5);

        Assert.That(setting, Is.Not.Null, "На старой базе строка настройки должна создаваться");
        Assert.That(setting!.Value, Is.EqualTo("5"));
        Assert.That(script.Script, Is.EqualTo("// new"));
    }

    [Test]
    public async Task ToDb_обновляет_existing_настройку()
    {
        await SeedSetting("StartScript", "1");

        var result = await _repository.ToDb(new ActionScript { Code = 4, Name = "main", Text = "// updated" });

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);

        var settings = await _ctx.Settings!.AsNoTracking().ToListAsync();

        Assert.That(settings, Has.Count.EqualTo(1));
        Assert.That(settings.Single().Value, Is.EqualTo("4"));
    }

    private async Task SeedSetting(string name, string value)
    {
        _ctx.Settings!.Add(new Settings { Id = 1, Name = name, Value = value });
        await _ctx.SaveChangesAsync();
    }

    private async Task SeedScript(int code, string text)
    {
        _ctx.ActionScripts!.Add(new ActScript { Id = 2, Code = code, Name = "Старт", Script = text });
        await _ctx.SaveChangesAsync();
    }
}
