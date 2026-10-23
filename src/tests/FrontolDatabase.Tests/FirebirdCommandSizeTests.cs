using Domain.Frontol.Interfaces;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FirebirdCommandSizeTests
{
    private const string ConnectionString = "database=localhost:dummy.fdb;user=sysdba;password=masterkey";

    [Test]
    public void Имя_настройки_ограничено_длиной_колонки()
    {
        using var ctx = Context();

        var length = ctx.Model.FindEntityType(typeof(Settings))!
            .FindProperty(nameof(Settings.Name))!
            .GetMaxLength();

        Assert.That(length, Is.EqualTo(100));
    }

    [Test]
    public void Чтение_SETTINGS_идёт_без_фильтра_по_именам()
    {
        using var ctx = Context();
        ctx.Settings = ctx.Set<Settings>();
        var repository = new SettingsRepository(
            new Mock<ILogger<SettingsRepository>>().Object,
            ctx,
            new Mock<IFrontolMainDb>().Object);

        var sql = repository.AllRowsQuery(tracking: false).ToQueryString();

        Assert.That(sql, Does.Not.Contain("8191"));
        Assert.That(sql, Does.Not.Contain("WHERE").IgnoreCase);
        Assert.That(sql, Does.Not.Contain("CHNG"));
        Assert.That(sql, Does.Not.Contain("BDOCODE"));
        Assert.That(sql, Does.Not.Contain("OWNERBDO"));
        Assert.That(sql, Does.Not.Contain("SYNCCATEG"));
        Assert.That(sql, Does.Not.Contain("INSCHNG"));
    }

    [Test]
    public void Параметр_имени_настройки_без_VARCHAR_8191()
    {
        using var ctx = Context();
        ctx.Settings = ctx.Set<Settings>();
        var name = "ControlSale";

        var sql = ctx.Settings.Where(row => row.Name == name).ToQueryString();

        Assert.That(sql, Does.Not.Contain("8191"));
    }

    [Test]
    public void Коды_режутся_пачками_ниже_лимита_Firebird()
    {
        var keys = Enumerable.Range(0, 201).ToList();
        var keyBatches = FirebirdQuery.Batches(keys, FirebirdQuery.KeyBatch).ToList();
        Assert.That(keyBatches.Select(batch => batch.Count), Is.EqualTo(new[] { 200, 1 }));
    }

    private static MainDbCtx Context()
    {
        var builder = new DbContextOptionsBuilder<MainDbCtx>();
        MainDbCtx.UseFrontolFirebird(builder, ConnectionString);
        return new MainDbCtx(builder.Options);
    }
}
