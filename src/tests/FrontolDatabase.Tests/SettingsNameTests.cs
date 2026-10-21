using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using FrontolDatabase.Mapping;
using FrontolDatabase.Parsers;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class SettingsNameTests
{
    [Test]
    public void Повтор_NAME_читается_из_строки_с_меньшим_Id()
    {
        var settings = new List<Settings>
        {
            new() { Id = 20, Name = "ControlSaleRemain", Value = "1" },
            new() { Id = 5, Name = "ControlSaleRemain", Value = "0" },
            new() { Id = 1, Name = null!, Value = "1" },
            new() { Id = 2, Name = "", Value = "1" }
        };

        var control = settings.ApplyFromSettings<GlobalControl>(Parse);

        Assert.That(control.ControlSaleRemain, Is.EqualTo(YesNoWareEnum.Ware));
    }

    [Test]
    public void Повтор_NAME_записывается_во_все_строки()
    {
        var rows = new List<Settings>
        {
            new() { Id = 20, Name = "ControlSaleRemain", Value = "2" },
            new() { Id = 5, Name = "ControlSaleRemain", Value = "2" },
            new() { Id = 1, Name = null!, Value = "2" }
        };
        var config = new GlobalControl { ControlSaleRemain = YesNoWareEnum.Ware };

        config.ApplyToSettings(rows, value => YesNoWareParsers.MapYesNoWareToDb((YesNoWareEnum)value));

        Assert.That(rows.Where(row => row.Name == "ControlSaleRemain").Select(row => row.Value), Is.All.EqualTo("0"));
        Assert.That(rows.Single(row => row.Id == 1).Value, Is.EqualTo("2"));
    }

    [Test]
    public async Task LoadParameters_пишет_повтор_NAME_во_все_строки()
    {
        await using var ctx = InMemory();
        ctx.Settings!.AddRange(
            new Settings { Id = 9, Name = "Foo", Value = "old" },
            new Settings { Id = 5, Name = "Foo", Value = "old" });
        await ctx.SaveChangesAsync();
        var repository = Repository(ctx);

        var result = await repository.LoadParameters([new FrontolParameter { Id = "Foo", Value = "new" }]);

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);
        var values = await ctx.Settings.Select(row => row.Value).ToListAsync();
        Assert.That(values, Is.All.EqualTo("new"));
    }

    [Test]
    public async Task SetSetting_пишет_повтор_NAME_во_все_строки()
    {
        await using var ctx = InMemory();
        ctx.Settings!.AddRange(
            new Settings { Id = 9, Name = "StartScript", Value = "1" },
            new Settings { Id = 5, Name = "StartScript", Value = "1" },
            new Settings { Id = 3, Name = "Other", Value = "keep" });
        await ctx.SaveChangesAsync();

        var result = await Repository(ctx).SetSetting("StartScript", "7");

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);
        var rows = await ctx.Settings.ToListAsync();
        Assert.That(rows.Where(row => row.Name == "StartScript").Select(row => row.Value), Is.All.EqualTo("7"));
        Assert.That(rows.Single(row => row.Name == "Other").Value, Is.EqualTo("keep"));
    }

    [Test]
    public async Task GetParameters_пропускает_пустое_имя()
    {
        await using var ctx = InMemory();
        ctx.Settings!.AddRange(
            new Settings { Id = 1, Name = "", Value = "empty" },
            new Settings { Id = 2, Name = "Foo", Value = "keep" });
        await ctx.SaveChangesAsync();

        var result = await Repository(ctx).GetParameters();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);
        Assert.That(result.Value.Select(parameter => parameter.Id), Is.EqualTo(new[] { "Foo" }));
    }

    private static object? Parse(string raw, Type type) => YesNoWareParsers.ParseYesNoWareEnum(raw);

    private static MainDbCtx InMemory()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new MainDbCtx(options);
        ctx.Settings = ctx.Set<Settings>();
        return ctx;
    }

    private static SettingsRepository Repository(MainDbCtx ctx) =>
        new(
            new Mock<ILogger<SettingsRepository>>().Object,
            ctx,
            new Mock<IFrontolMainDb>().Object);
}
