using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Cashiers;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class CashiersRepositoryTests
{
    private MainDbCtx _db = null!;
    private CashiersRepository _repository = null!;
    private int _nextId = 9000;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MainDbCtx(options);
        _db.Users = _db.Set<User>();
        _db.UserProfiles = _db.Set<Profile>();
        _db.CustomDb = _db.Set<CustomDb>();
        _db.CustomDb.Add(new CustomDb { Code = 7 });
        _db.UserProfiles.Add(new Profile { Id = 40, Code = 4, Name = "кассир" });
        _db.SaveChanges();

        _nextId = 9000;
        var mainDb = new Mock<IFrontolMainDb>();
        mainDb.Setup(m => m.NextChangeId()).Returns(() => Task.FromResult(_nextId++));

        _repository = new CashiersRepository(
            new Mock<ILogger<CashiersRepository>>().Object,
            _db,
            mainDb.Object);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Apply_новая_строка_пишет_CODE_профиль_и_пароль_как_есть()
    {
        var result = await _repository.Apply(100, [Cashier(5, "Анна", 4, "secret", "4601")]);

        Assert.That(result.IsSuccess, Is.True);
        var user = _db.Users!.Single(row => row.Code == 105);
        Assert.That(user.Id, Is.EqualTo(9000));
        Assert.That(user.Name, Is.EqualTo("Анна"));
        Assert.That(user.ProfileId, Is.EqualTo(40));
        Assert.That(user.Pswrd, Is.EqualTo("5ebe2294ecd0e0f08eab7690d2a6ee69"));
        Assert.That(user.Card, Is.EqualTo("4601"));
        Assert.That(user.Deleted, Is.EqualTo(0));
        Assert.That(user.BdoCode, Is.EqualTo(7));
        Assert.That(user.ChangeCount, Is.EqualTo(9000));
    }

    [Test]
    public async Task Apply_существующий_CODE_обновляет_строку_и_не_меняет_Id()
    {
        _db.Users!.Add(new User
        {
            Id = 3,
            Code = 105,
            Name = "старый",
            ProfileId = 1,
            Pswrd = "old",
            Card = "old-card",
            Deleted = 1,
            BdoCode = 2,
            ChangeCount = 10
        });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(100, [Cashier(5, "Анна", 4, "secret", "4601")]);

        Assert.That(result.IsSuccess, Is.True);
        var user = _db.Users!.Single();
        Assert.That(user.Id, Is.EqualTo(3));
        Assert.That(user.Name, Is.EqualTo("Анна"));
        Assert.That(user.ProfileId, Is.EqualTo(40));
        Assert.That(user.Pswrd, Is.EqualTo("5ebe2294ecd0e0f08eab7690d2a6ee69"));
        Assert.That(user.Card, Is.EqualTo("4601"));
        Assert.That(user.Deleted, Is.EqualTo(0));
        Assert.That(user.BdoCode, Is.EqualTo(2));
        Assert.That(user.ChangeCount, Is.EqualTo(9000));
    }

    [Test]
    public async Task Apply_пустой_пароль_пишет_md5_пустой_строки()
    {
        var result = await _repository.Apply(100, [Cashier(5, "Анна", 4, "", "4601")]);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_db.Users!.Single().Pswrd, Is.EqualTo("d41d8cd98f00b204e9800998ecf8427e"));
    }

    [Test]
    public async Task Apply_код_не_больше_смещения_пропускает_строку()
    {
        _db.Users!.Add(new User { Id = 8, Code = 100, Name = "на границе", Deleted = 0, ChangeCount = 10 });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(100, [Cashier(0, "пусто", 4, "secret", "1")]);

        Assert.That(result.IsSuccess, Is.True);
        var border = _db.Users!.Single();
        Assert.That(border.Code, Is.EqualTo(100));
        Assert.That(border.Name, Is.EqualTo("на границе"));
        Assert.That(border.ChangeCount, Is.EqualTo(10));
    }

    [Test]
    public async Task Apply_не_трогает_пользователя_не_выше_смещения()
    {
        _db.Users!.Add(new User { Id = 8, Code = 1, Name = "админ", Deleted = 0, ChangeCount = 10 });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(100, [Cashier(5, "Анна", 4, "secret", "4601")]);

        Assert.That(result.IsSuccess, Is.True);
        var other = _db.Users!.Single(row => row.Code == 1);
        Assert.That(other.Name, Is.EqualTo("админ"));
        Assert.That(other.Deleted, Is.EqualTo(0));
        Assert.That(other.ChangeCount, Is.EqualTo(10));
    }

    [Test]
    public async Task Apply_кассира_нет_в_пакете_удаляет_строку()
    {
        _db.Users!.Add(new User { Id = 8, Code = 150, Name = "бывший", Deleted = 0, ChangeCount = 10 });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(100, [Cashier(5, "Анна", 4, "secret", "4601")]);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_db.Users!.Any(row => row.Code == 150), Is.False);
        Assert.That(_db.Users!.Single(row => row.Code == 105).Deleted, Is.EqualTo(0));
    }

    [Test]
    public async Task Apply_пустой_пакет_удаляет_кассиров_выше_смещения()
    {
        _db.Users!.Add(new User { Id = 8, Code = 150, Name = "бывший", Deleted = 0 });
        _db.Users!.Add(new User { Id = 1, Code = 1, Name = "админ", Deleted = 0, ChangeCount = 3 });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(100, []);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_db.Users!.Any(row => row.Code == 150), Is.False);
        var admin = _db.Users!.Single(row => row.Code == 1);
        Assert.That(admin.Deleted, Is.EqualTo(0));
        Assert.That(admin.ChangeCount, Is.EqualTo(3));
    }

    [Test]
    public async Task Apply_без_профиля_пишет_кассира_с_ProfileId_0()
    {
        var result = await _repository.Apply(100, [Cashier(5, "Анна", 99, "secret", "4601")]);

        Assert.That(result.IsSuccess, Is.True);
        var user = _db.Users!.Single();
        Assert.That(user.Code, Is.EqualTo(105));
        Assert.That(user.ProfileId, Is.EqualTo(0));
    }

    [Test]
    public async Task Apply_без_CustomDb_ставит_BdoCode_0()
    {
        _db.CustomDb!.RemoveRange(_db.CustomDb);
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(100, [Cashier(5, "Анна", 4, "secret", "4601")]);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_db.Users!.Single().BdoCode, Is.EqualTo(0));
    }

    [Test]
    public async Task Apply_без_таблицы_USER_возвращает_ошибку()
    {
        _db.Users = null;

        var result = await _repository.Apply(100, [Cashier(5, "Анна", 4, "secret", "4601")]);

        Assert.That(result.IsFailure, Is.True);
    }

    private static CashierItem Cashier(int code, string name, int profileCode, string password, string barcode) =>
        new()
        {
            Code = code,
            Name = name,
            ProfileCode = profileCode,
            Password = password,
            Barcode = barcode
        };
}
