using Domain.Frontol.Enums;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class SalesDocumentsRepositoryTests
{
    private MainDbCtx _dbContext = null!;
    private SalesDocumentsRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MainDbCtx(options);
        _dbContext.Documents = _dbContext.Set<Document>();
        _dbContext.DocKinds = _dbContext.Set<DocKind>();
        _dbContext.Transactions = _dbContext.Set<TranzT>();
        _dbContext.Wares = _dbContext.Set<SprT>();
        _dbContext.Users = _dbContext.Set<User>();
        _dbContext.Payments = _dbContext.Set<Payment>();
        _dbContext.DocKinds.Add(new DocKind { Id = 1, Code = 1, Name = "Продажа" });
        _dbContext.SaveChanges();

        _repository = new SalesDocumentsRepository(
            new Mock<ILogger<SalesDocumentsRepository>>().Object,
            _dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task After_возвращает_закрытые_продажи_после_курсора()
    {
        await SeedWare(2, "Кофе");
        await SeedCashier(2151827, 12, "Иванов");
        await SeedClosedSale(100, 200, 50, closeDate: DateTime.Today.AddDays(-1));
        await SeedClosedSale(101, 201, 98.13);
        await SeedDeferred(102, 202, 10);

        var result = await _repository.After(100, 50);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(d => d.DocumentNumber), Is.EqualTo(new[] { 101L }));
        var document = result.Value.Single();
        Assert.That(document.CheckNumber, Is.EqualTo(201));
        Assert.That(document.DocumentType, Is.EqualTo(1));
        Assert.That(document.CashierCode, Is.EqualTo("12"));
        Assert.That(document.CashierName, Is.EqualTo("Иванов"));
        Assert.That(document.Positions.Single().Name, Is.EqualTo("Кофе"));
    }

    [Test]
    public async Task After_пропускает_непродажные_типы()
    {
        await SeedWare(2, "Кофе");
        await SeedClosedSale(101, 201, 10, ReceiptTypeEnum.CahsIn);

        var result = await _repository.After(0, 50);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task After_включает_возврат_и_коррекцию()
    {
        await SeedWare(2, "Кофе");
        await SeedDocKind(2, 14);
        await SeedDocKind(3, 15);
        await SeedClosedSale(101, 201, 10, ReceiptTypeEnum.ReturnSell, documentKindId: 2);
        await SeedClosedSale(102, 202, 20, ReceiptTypeEnum.SellCorrection, documentKindId: 3);

        var result = await _repository.After(0, 50);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(d => d.DocumentType), Is.EqualTo(new[] { 14, 15 }));
    }

    [Test]
    public async Task After_инвертирует_знак_сторно()
    {
        await SeedWare(2, "Кофе");
        await SeedClosedWithStorno(101, 201);

        var result = await _repository.After(0, 50);

        Assert.That(result.IsSuccess, Is.True);
        var quantities = result.Value.Single().Positions.Select(p => p.Quantity);
        Assert.That(quantities, Is.EqualTo(new[] { 1d, -1d, 1d }));
    }

    [Test]
    public async Task After_передаёт_сторно_и_суммы_без_скидки()
    {
        await SeedWare(2, "Кофе");
        await SeedClosedWithDiscountAndStorno(101, 201);

        var result = await _repository.After(0, 50);

        Assert.That(result.IsSuccess, Is.True);
        var positions = result.Value.Single().Positions;
        Assert.That(positions[0].Storno, Is.False);
        Assert.That(positions[0].Price, Is.EqualTo(80));
        Assert.That(positions[0].Sum, Is.EqualTo(80));
        Assert.That(positions[0].PriceWd, Is.EqualTo(100));
        Assert.That(positions[0].SumWd, Is.EqualTo(100));
        Assert.That(positions[1].Storno, Is.True);
        Assert.That(positions[1].Quantity, Is.EqualTo(-1d));
        Assert.That(positions[1].Sum, Is.EqualTo(-80d));
        Assert.That(positions[1].PriceWd, Is.EqualTo(100));
        Assert.That(positions[1].SumWd, Is.EqualTo(-100d));
    }

    [Test]
    public async Task After_ограничивает_пачку()
    {
        await SeedWare(2, "Кофе");
        await SeedClosedSale(101, 201, 10);
        await SeedClosedSale(102, 202, 10);
        await SeedClosedSale(103, 203, 10);

        var result = await _repository.After(0, 2);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(d => d.DocumentNumber), Is.EqualTo(new[] { 101L, 102L }));
    }

    [Test]
    public async Task After_при_нулевом_курсоре_только_сегодняшние()
    {
        await SeedWare(2, "Кофе");
        await SeedClosedSale(90, 190, 10, closeDate: DateTime.Today.AddDays(-1));
        await SeedClosedSale(101, 201, 20);

        var result = await _repository.After(0, 50);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(d => d.DocumentNumber), Is.EqualTo(new[] { 101L }));
    }

    [Test]
    public async Task After_читает_оплаты_из_TRANZT()
    {
        await SeedWare(2, "Кофе");
        await SeedPaymentKind(1, "Наличные");
        await SeedClosedSale(101, 201, 98.13, paymentCode: 1, paymentSum: 98.13);

        var result = await _repository.After(0, 50);

        Assert.That(result.IsSuccess, Is.True);
        var payments = result.Value.Single().Payments;
        Assert.That(payments.Select(p => (p.PaymentCode, p.PaymentName, p.Sum)),
            Is.EqualTo(new[] { (1, "Наличные", 98.13) }));
    }

    [Test]
    public void WaresByCodes_sql_для_нескольких_кодов_без_FALSE()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseFirebird("database=localhost:dummy.fdb;user=sysdba;password=masterkey")
            .Options;

        using var ctx = new MainDbCtx(options);
        ctx.Wares = ctx.Set<SprT>();
        var repository = new SalesDocumentsRepository(
            new Mock<ILogger<SalesDocumentsRepository>>().Object,
            ctx);

        var sql = repository.WaresByCodesQuery([2, 3]).ToQueryString();

        Assert.That(sql, Does.Not.Contain("FALSE").IgnoreCase);
        Assert.That(sql, Does.Contain("CODE"));
    }

    private async Task SeedWare(int code, string name)
    {
        _dbContext.Wares!.Add(new SprT
        {
            Id = code,
            Code = code,
            Name = name,
            IsWare = 1
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedPaymentKind(int code, string name)
    {
        _dbContext.Payments!.Add(new Payment
        {
            Id = code,
            Code = code,
            Name = name,
            Operation = PaymentOperationEnum.Cash,
            Deleted = 0
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedCashier(int id, int code, string name)
    {
        _dbContext.Users!.Add(new User
        {
            Id = id,
            Code = code,
            Name = name
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedDocKind(int id, int code)
    {
        _dbContext.DocKinds!.Add(new DocKind
        {
            Id = id,
            Code = code,
            Name = $"Вид {code}"
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedClosedSale(
        long id,
        int checkNumber,
        double summ,
        ReceiptTypeEnum type = ReceiptTypeEnum.Sell,
        DateTime? closeDate = null,
        int? paymentCode = null,
        double? paymentSum = null,
        int documentKindId = 1)
    {
        _dbContext.Documents!.Add(Closed(id, checkNumber, summ, DocumentStateEnum.Closed, type, closeDate, documentKindId));
        var rows = new List<TranzT>
        {
            Open(id + 1000, id, 1, summ),
            Ware(id + 2000, id, wareCode: 2, pos: 1, price: summ)
        };
        if (paymentCode is not null)
            rows.Add(Pay(id + 3000, id, paymentCode.Value, paymentSum ?? summ));
        _dbContext.Transactions!.AddRange(rows);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedClosedWithStorno(long id, int checkNumber)
    {
        _dbContext.Documents!.Add(Closed(id, checkNumber, 55.41, DocumentStateEnum.Closed, ReceiptTypeEnum.Sell));
        _dbContext.Transactions!.AddRange(
            Open(id + 1000, id, 1, 55.41),
            Ware(id + 2000, id, wareCode: 2, pos: 1, price: 98.13),
            Storno(id + 2001, id, wareCode: 2, pos: 1, price: 98.13),
            Ware(id + 2002, id, wareCode: 2, pos: 2, price: 55.41));
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedClosedWithDiscountAndStorno(long id, int checkNumber)
    {
        _dbContext.Documents!.Add(Closed(id, checkNumber, 0, DocumentStateEnum.Closed, ReceiptTypeEnum.Sell));
        _dbContext.Transactions!.AddRange(
            Open(id + 1000, id, 1, 0),
            Ware(id + 2000, id, wareCode: 2, pos: 1, price: 80, priceWd: 100),
            Storno(id + 2001, id, wareCode: 2, pos: 1, price: 80, priceWd: 100));
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedDeferred(long id, int checkNumber, double summ)
    {
        _dbContext.Documents!.Add(Closed(id, checkNumber, summ, DocumentStateEnum.Deffered, ReceiptTypeEnum.Sell));
        await _dbContext.SaveChangesAsync();
    }

    private static Document Closed(
        long id,
        int checkNumber,
        double summ,
        DocumentStateEnum state,
        ReceiptTypeEnum type,
        DateTime? closeDate = null,
        int documentKindId = 1)
    {
        var day = (closeDate ?? DateTime.Today).Date;
        return new Document
        {
            Id = id,
            DocumentKindId = documentKindId,
            CheckNumber = checkNumber,
            OpenDate = day,
            OpenTime = day.AddHours(12),
            OpenUserId = 2151827,
            CloseDate = day,
            CloseTime = day.AddHours(12).AddMinutes(5),
            CloseUserId = 2151827,
            State = state,
            Summ = summ,
            SummWd = summ,
            ChequeType = type,
            IsFiscal = 1
        };
    }

    private static TranzT Open(long id, long documentId, double quantity, double summ) =>
        Base(id, documentId, TranzTypeEnum.OpenDocument, quantity, summ);

    private static TranzT Ware(long id, long documentId, int wareCode, int pos, double price, double? priceWd = null)
    {
        var row = Base(id, documentId, TranzTypeEnum.WareFromCatalog, 1, price);
        row.WareCode = wareCode;
        row.Price = price;
        row.PriceWd = priceWd ?? price;
        row.SummWd = priceWd ?? price;
        row.PosId = pos;
        row.PosNumb = pos;
        row.Barcode = "4600000000001";
        return row;
    }

    private static TranzT Pay(long id, long documentId, int paymentCode, double summ)
    {
        var row = Base(id, documentId, TranzTypeEnum.Payment, 0, summ);
        row.Price = summ;
        row.Info = paymentCode;
        row.PosNumb = 2;
        return row;
    }

    private static TranzT Storno(long id, long documentId, int wareCode, int pos, double price, double? priceWd = null)
    {
        var row = Ware(id, documentId, wareCode, pos, price, priceWd);
        row.TranzType = TranzTypeEnum.StornoFromCatalog;
        return row;
    }

    private static TranzT Base(long id, long documentId, TranzTypeEnum type, double quantity, double summ) =>
        new()
        {
            Id = id,
            DocumentId = documentId,
            TranzDate = new DateTime(2026, 9, 8),
            TranzTime = new DateTime(2026, 9, 8, 12, 0, 0),
            TranzHour = 12,
            TranzType = type,
            Quantity = quantity,
            Summ = summ,
            SummWd = summ
        };
}
