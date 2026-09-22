using System.Text.Json;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Wares;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class WaresRepositoryTests
{
    private MainDbCtx _db = null!;
    private WaresRepository _repository = null!;
    private int _nextId = 9000;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MainDbCtx(options);
        _db.Wares = _db.Set<SprT>();
        _db.Remains = _db.Set<Remain>();
        _db.RemainDs = _db.Set<RemainD>();
        _db.PriceDatas = _db.Set<PriceData>();
        _db.BarCodes = _db.Set<BarCode>();
        _db.TaxGroups = _db.Set<TaxGroup>();
        _db.PrintGroups = _db.Set<PrintGroup>();
        _db.CustomDb = _db.Set<CustomDb>();

        _nextId = 9000;
        var mainDb = new Mock<IFrontolMainDb>();
        mainDb.Setup(m => m.NextChangeId()).Returns(() => Task.FromResult(_nextId++));

        _repository = new WaresRepository(
            new Mock<ILogger<WaresRepository>>().Object,
            _db,
            mainDb.Object);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Apply_группа_и_товар_ставит_ParentId()
    {
        var result = await _repository.Apply(
            [Group("1", "напитки")],
            [Ware(10, "1", "кола")]);

        Assert.That(result.IsSuccess, Is.True);
        var group = _db.Wares!.Single(w => w.Code == 1 && w.IsWare == 0);
        var ware = _db.Wares!.Single(w => w.Code == 10 && w.IsWare == 1);
        Assert.That(ware.ParentId, Is.EqualTo(group.Id));
    }

    [Test]
    public async Task Apply_цена_без_остатка_пишет_Remain_и_PriceData()
    {
        var extra = Extra("""{"price":99.5}""");

        var result = await _repository.Apply([], [Ware(10, "1", "кола", extra)]);

        Assert.That(result.IsSuccess, Is.True);
        var ware = _db.Wares!.Single(w => w.Code == 10);
        var remain = _db.Remains!.Single(r => r.WareId == ware.Id);
        Assert.That(remain.UseRemain, Is.EqualTo(0));
        var price = _db.PriceDatas!.Single(p => p.RemainId == remain.Id);
        Assert.That(price.Price, Is.EqualTo(99.5));
        Assert.That(price.ActDateTime, Is.EqualTo(new DateTime(1899, 12, 30)));
    }

    [Test]
    public async Task Apply_loadRemain_пишет_RemainD()
    {
        var extra = Extra("""{"loadRemain":true,"remainQuantity":5}""");

        var result = await _repository.Apply([], [Ware(10, "1", "кола", extra)]);

        Assert.That(result.IsSuccess, Is.True);
        var ware = _db.Wares!.Single(w => w.Code == 10);
        var remain = _db.Remains!.Single(r => r.WareId == ware.Id);
        Assert.That(remain.UseRemain, Is.EqualTo(1));
        var delta = _db.RemainDs!.Single(d => d.RemainId == remain.Id);
        Assert.That(delta.Delta, Is.EqualTo(5));
        Assert.That(delta.DType, Is.EqualTo(1));
        Assert.That(delta.DocumentId, Is.EqualTo(0));
    }

    [Test]
    public async Task Apply_штрихкод_удаляет_старую_строку_по_значению()
    {
        _db.Wares!.Add(new SprT { Id = 100, Code = 1, Name = "старый", IsWare = 1 });
        _db.BarCodes!.Add(new BarCode { Id = 50, WareId = 100, Barcode = "460123", Factor = 1 });
        await _db.SaveChangesAsync();

        var extra = Extra("""{"barcodes":[{"barcode":"460123","factor":1}]}""");
        var result = await _repository.Apply([], [Ware(2, "1", "новый", extra)]);

        Assert.That(result.IsSuccess, Is.True);
        var ware = _db.Wares!.Single(w => w.Code == 2);
        var rows = _db.BarCodes!.Where(b => b.Barcode == "460123").ToList();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].WareId, Is.EqualTo(ware.Id));
    }

    private static WareGroup Group(string code, string name) => new()
    {
        GroupCode = code,
        Name = name
    };

    private static WareItem Ware(int code, string groupCode, string name, JsonElement extra = default) => new()
    {
        WareCode = code,
        GroupCode = groupCode,
        Name = name,
        Extra = extra
    };

    private static JsonElement Extra(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
