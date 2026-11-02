using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class TouchLayoutRepositoryTests
{
    private MainDbCtx _db = null!;
    private TouchLayoutRepository _repository = null!;
    private Mock<IFrontolMainDb> _mainDb = null!;
    private int _nextId = 9000;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MainDbCtx(options);
        _db.TouchLayouts = _db.Set<TouchLayt>();
        _db.SaveChanges();

        _nextId = 9000;
        _mainDb = new Mock<IFrontolMainDb>();
        _mainDb.Setup(m => m.NextChangeId()).Returns(() => Task.FromResult(_nextId++));

        _repository = new TouchLayoutRepository(
            new Mock<ILogger<TouchLayoutRepository>>().Object,
            _db,
            _mainDb.Object);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void Модель_пакета_не_содержит_Id()
    {
        Assert.That(typeof(TouchLayout).GetProperty("Id"), Is.Null);
    }

    [Test]
    public async Task Read_отдаёт_код_имя_и_данные()
    {
        _db.TouchLayouts!.Add(new TouchLayt { Id = 10, Code = 1, Name = "Основной", Data = [1, 2, 3] });
        await _db.SaveChangesAsync();

        var result = await _repository.Read();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        var layout = result.Value.Layout!.Single();
        Assert.That(layout.Code, Is.EqualTo(1));
        Assert.That(layout.Name, Is.EqualTo("Основной"));
        Assert.That(layout.Data, Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task Read_пустой_таблицы_отдаёт_пустой_список()
    {
        var result = await _repository.Read();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(result.Value.Layout, Is.Empty);
    }

    [Test]
    public async Task Read_без_таблицы_отдаёт_null()
    {
        _db.TouchLayouts = null;

        var result = await _repository.Read();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(result.Value.Layout, Is.Null);
    }

    [Test]
    public async Task Apply_новая_запись_берёт_Id_из_генератора()
    {
        var result = await _repository.Apply([new TouchLayout { Code = 4, Name = "Касса", Data = [9] }]);

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        var row = _db.TouchLayouts!.Single();
        Assert.That(row.Id, Is.EqualTo(9000));
        Assert.That(row.Code, Is.EqualTo(4));
        Assert.That(row.Name, Is.EqualTo("Касса"));
        Assert.That(row.Data, Is.EqualTo(new byte[] { 9 }));
        _mainDb.Verify(m => m.NextChangeId(), Times.Once);
    }

    [Test]
    public async Task Apply_по_коду_обновляет_строку_и_не_меняет_Id()
    {
        _db.TouchLayouts!.Add(new TouchLayt { Id = 10, Code = 1, Name = "Старый", Data = [1] });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply([new TouchLayout { Code = 1, Name = "Новый", Data = [2, 3] }]);

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        var row = _db.TouchLayouts!.Single();
        Assert.That(row.Id, Is.EqualTo(10));
        Assert.That(row.Name, Is.EqualTo("Новый"));
        Assert.That(row.Data, Is.EqualTo(new byte[] { 2, 3 }));
        _mainDb.Verify(m => m.NextChangeId(), Times.Never);
    }

    [Test]
    public async Task Apply_не_удаляет_коды_которых_нет_в_пакете()
    {
        _db.TouchLayouts!.Add(new TouchLayt { Id = 10, Code = 1, Name = "Оставить" });
        _db.TouchLayouts.Add(new TouchLayt { Id = 11, Code = 2, Name = "Тоже" });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply([new TouchLayout { Code = 1, Name = "Обновить" }]);

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(_db.TouchLayouts!.Select(row => row.Code).OrderBy(code => code), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(_db.TouchLayouts!.Single(row => row.Code == 1).Name, Is.EqualTo("Обновить"));
        Assert.That(_db.TouchLayouts!.Single(row => row.Code == 2).Name, Is.EqualTo("Тоже"));
    }

    [Test]
    public async Task Apply_пустой_список_и_null_не_трогают_таблицу()
    {
        _db.TouchLayouts!.Add(new TouchLayt { Id = 10, Code = 1, Name = "Оставить" });
        await _db.SaveChangesAsync();

        var empty = await _repository.Apply([]);
        var missing = await _repository.Apply(null);

        Assert.That(empty.IsSuccess, Is.True);
        Assert.That(missing.IsSuccess, Is.True);
        Assert.That(_db.TouchLayouts!.Single().Name, Is.EqualTo("Оставить"));
        _mainDb.Verify(m => m.NextChangeId(), Times.Never);
    }

    [Test]
    public async Task Apply_повтор_кода_берёт_последний_элемент()
    {
        var result = await _repository.Apply(
        [
            new TouchLayout { Code = 1, Name = "Первый", Data = [1] },
            new TouchLayout { Code = 1, Name = "Второй", Data = [2] }
        ]);

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        var row = _db.TouchLayouts!.Single();
        Assert.That(row.Name, Is.EqualTo("Второй"));
        Assert.That(row.Data, Is.EqualTo(new byte[] { 2 }));
        _mainDb.Verify(m => m.NextChangeId(), Times.Once);
    }

    [Test]
    public async Task Apply_длинное_имя_не_пишет_строку()
    {
        _db.TouchLayouts!.Add(new TouchLayt { Id = 10, Code = 1, Name = "Оставить" });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply([new TouchLayout { Code = 3, Name = new string('Я', 101) }]);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("100"));
        Assert.That(_db.TouchLayouts!.Select(row => row.Code), Is.EqualTo(new[] { 1 }));
        _mainDb.Verify(m => m.NextChangeId(), Times.Never);
    }

    [Test]
    public async Task Apply_без_таблицы_возвращает_ошибку()
    {
        _db.TouchLayouts = null;

        var result = await _repository.Apply([new TouchLayout { Code = 1, Name = "Касса" }]);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("TOUCHLAYT"));
        _mainDb.Verify(m => m.NextChangeId(), Times.Never);
    }

    [Test]
    public void Запрос_макетов_содержит_колонки_контракта_и_двоичный_BLOB()
    {
        var builder = new DbContextOptionsBuilder<MainDbCtx>();
        MainDbCtx.UseFrontolFirebird(builder, "database=localhost:dummy.fdb;user=sysdba;password=masterkey");

        using var ctx = new MainDbCtx(builder.Options, new FrontolSchemaProvider(FrontolSchema.Unknown));
        ctx.TouchLayouts = ctx.Set<TouchLayt>();

        var sql = ctx.TouchLayouts!.ToQueryString();
        Assert.That(sql, Does.Contain("TOUCHLAYT"));
        Assert.That(sql, Does.Contain("CODE"));
        Assert.That(sql, Does.Contain("NAME"));
        Assert.That(sql, Does.Contain("DATA"));
        Assert.That(sql, Does.Not.Contain("CHNG"));
        Assert.That(sql, Does.Not.Contain("BDOCODE"));
        Assert.That(
            ctx.Model.FindEntityType(typeof(TouchLayt))!.FindProperty(nameof(TouchLayt.Data))!.GetColumnType(),
            Is.EqualTo("BLOB"));
    }

    [Test]
    public void База_без_TOUCHLAYT_исключает_сущность()
    {
        var builder = new DbContextOptionsBuilder<MainDbCtx>();
        MainDbCtx.UseFrontolFirebird(builder, "database=localhost:dummy.fdb;user=sysdba;password=masterkey");
        builder.ReplaceService<IModelCacheKeyFactory, FrontolModelCacheKeyFactory>();

        using var ctx = new MainDbCtx(
            builder.Options,
            new FrontolSchemaProvider(FrontolSchema.ForTests(("PROFILE", "ID"))));

        Assert.That(ctx.Model.FindEntityType(typeof(TouchLayt)), Is.Null);
    }
}
