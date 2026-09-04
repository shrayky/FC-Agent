using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.DeferredReceipts;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class DeferredReceiptsRepositoryTests
{
    private MainDbCtx _dbContext = null!;
    private DeferredReceiptsRepository _repository = null!;
    private Mock<IFrontolMainDb> _mainDb = null!;
    private int _nextId = 9000;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MainDbCtx(options);
        _dbContext.Documents = _dbContext.Set<Document>();
        _dbContext.Transactions = _dbContext.Set<TranzT>();
        _dbContext.Payments = _dbContext.Set<Payment>();
        _dbContext.Wares = _dbContext.Set<SprT>();
        _dbContext.PrintGroups = _dbContext.Set<PrintGroup>();

        _nextId = 9000;
        _mainDb = new Mock<IFrontolMainDb>();
        _mainDb.Setup(m => m.NextChangeId()).Returns(() => Task.FromResult(_nextId++));

        _repository = new DeferredReceiptsRepository(
            new Mock<ILogger<DeferredReceiptsRepository>>().Object,
            _dbContext,
            _mainDb.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task List_возвращает_только_отложенные()
    {
        await SeedWare(2, "Кофе");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithoutPayment();
        await SeedClosedWithoutPrintGroup();

        var result = await _repository.List();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Receipts.Select(r => r.Id), Is.EqualTo(new[] { 1746L }));
        Assert.That(result.Value.PaymentKinds.Select(p => p.Code), Is.EqualTo(new[] { 1 }));
        Assert.That(result.Value.Receipts.Single().Positions.Select(p => p.PrintGroupCode), Is.EqualTo(new[] { 1, 1 }));
    }

    [Test]
    public async Task Count_считает_только_отложенные()
    {
        await SeedDeferredWithoutPayment();
        await SeedDeferredWithPartialPayment();
        await SeedClosedWithoutPrintGroup();

        var result = await _repository.Count();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(2));
    }

    [Test]
    public async Task Count_без_отложенных_возвращает_0()
    {
        await SeedClosedWithoutPrintGroup();

        var result = await _repository.Count();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(0));
    }

    [Test]
    public async Task List_передаёт_группы_печати_с_именами()
    {
        await SeedWare(2, "Кофе");
        await SeedPayment(1, "Наличные");
        await SeedPrintGroup(10, 1, "Кухня");
        await SeedDeferredWithoutPayment();

        var result = await _repository.List();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.PrintGroups.Select(g => (g.Id, g.Code, g.Name)), Is.EqualTo(new[] { (10, 1, "Кухня") }));
    }

    [Test]
    public async Task List_читает_оплату_с_пустыми_необязательными_полями()
    {
        await SeedWare(2, "Кофе");
        await SeedPayment(1, "Наличные", printGroupId: null, isFiscal: null, fiscalOperation: null, ecrPayment: null);
        await SeedDeferredWithoutPayment();

        var result = await _repository.List();

        Assert.That(result.IsSuccess, Is.True);
        var kind = result.Value.PaymentKinds.Single();
        Assert.That(kind.Code, Is.EqualTo(1));
        Assert.That(kind.PrintGroupId, Is.EqualTo(0));
        Assert.That(kind.PrintGroupCode, Is.EqualTo(0));
    }

    [Test]
    public async Task List_подставляет_имя_из_SprT()
    {
        await SeedWare(2, "Кофе");
        await SeedWare(3, "Чай");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithoutPayment();

        var result = await _repository.List();

        Assert.That(result.IsSuccess, Is.True);
        var positions = result.Value.Receipts.Single().Positions;
        Assert.That(positions.Select(p => p.Name), Is.EqualTo(new[] { "Кофе", "Чай" }));
        Assert.That(positions.Select(p => p.WareCode), Is.EqualTo(new[] { 2, 3 }));
    }

    [Test]
    public async Task List_считает_остаток_по_транзакциям_40()
    {
        await SeedWare(2, "Кофе");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithPartialPayment();

        var result = await _repository.List();

        Assert.That(result.IsSuccess, Is.True);
        var receipt = result.Value.Receipts.Single();
        Assert.That(receipt.PaidSumm, Is.EqualTo(10).Within(0.001));
        Assert.That(receipt.RemainSumm, Is.EqualTo(88.13).Within(0.001));
        Assert.That(receipt.Payments, Has.Count.EqualTo(1));
        Assert.That(receipt.HasPrintGroup, Is.True);
    }

    [Test]
    public async Task Cancel_меняет_статус_и_пишет_транзакцию_56()
    {
        await SeedWare(2, "Кофе");
        await SeedWare(3, "Чай");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithoutPayment();

        var result = await _repository.Cancel(1746);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Id, Is.EqualTo(1746));

        var document = await _dbContext.Documents!.AsNoTracking().SingleAsync(d => d.Id == 1746);
        Assert.That(document.State, Is.EqualTo(DocumentStateEnum.Canceled));

        var cancel = await _dbContext.Transactions!.AsNoTracking()
            .SingleAsync(t => t.DocumentId == 1746 && t.TranzType == TranzTypeEnum.Cancel);
        Assert.That(cancel.Id, Is.EqualTo(9000));
        Assert.That(cancel.Summ, Is.EqualTo(153.54).Within(0.001));
        _mainDb.Verify(m => m.NextChangeId(), Times.Once);
    }

    [Test]
    public async Task Cancel_отклоняет_не_отложенный()
    {
        await SeedClosedWithoutPrintGroup();

        var result = await _repository.Cancel(1787);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Документ не отложен"));
    }

    [Test]
    public async Task AddPayment_без_ГП_пишет_только_40()
    {
        await SeedWare(2, "Кофе");
        await SeedWare(3, "Чай");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithoutPayment(printGroupCode: 0, printGroupClose: 0);

        var result = await _repository.AddPayment(1746, PayItems(20));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.PaidSumm, Is.EqualTo(20).Within(0.001));

        var payments = await _dbContext.Transactions!.AsNoTracking()
            .Where(t => t.DocumentId == 1746 && (t.TranzType == TranzTypeEnum.Payment || t.TranzType == TranzTypeEnum.PaymentByPrintGroup))
            .ToListAsync();

        Assert.That(payments, Has.Count.EqualTo(1));
        Assert.That(payments[0].TranzType, Is.EqualTo(TranzTypeEnum.Payment));
        Assert.That(payments[0].Id, Is.EqualTo(9000));
        Assert.That(payments[0].Info, Is.EqualTo(1));
        Assert.That(payments[0].PosNumb, Is.EqualTo(2));
        Assert.That(payments[0].DiscountEnabled, Is.EqualTo(207));
        Assert.That(payments[0].Summ, Is.EqualTo(20).Within(0.001));

        var document = await _dbContext.Documents!.AsNoTracking().SingleAsync(d => d.Id == 1746);
        Assert.That(document.LastPaymNum, Is.EqualTo(2));
        _mainDb.Verify(m => m.NextChangeId(), Times.Once);
    }

    [Test]
    public async Task AddPayment_с_ГП_пишет_40_и_43()
    {
        await SeedWare(2, "Кофе");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithPartialPayment();

        var result = await _repository.AddPayment(1753, PayItems(15, printGroupCode: 1));

        Assert.That(result.IsSuccess, Is.True);

        var added = await _dbContext.Transactions!.AsNoTracking()
            .Where(t => t.DocumentId == 1753 && t.Id >= 9000)
            .OrderBy(t => t.Id)
            .ToListAsync();

        Assert.That(added, Has.Count.EqualTo(2));
        Assert.That(added[0].TranzType, Is.EqualTo(TranzTypeEnum.Payment));
        Assert.That(added[0].PrintGroupClose, Is.EqualTo(0));
        Assert.That(added[1].TranzType, Is.EqualTo(TranzTypeEnum.PaymentByPrintGroup));
        Assert.That(added[1].PrintGroupClose, Is.EqualTo(1));
        Assert.That(added[1].Id, Is.EqualTo(9001));
        _mainDb.Verify(m => m.NextChangeId(), Times.Exactly(2));
    }

    [Test]
    public async Task AddPayment_больше_остатка_ошибка()
    {
        await SeedWare(2, "Кофе");
        await SeedWare(3, "Чай");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithoutPayment();

        var result = await _repository.AddPayment(1746, PayItems(200));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Сумма оплаты больше остатка"));
    }

    [Test]
    public async Task Close_без_полной_оплаты_ошибка()
    {
        await SeedWare(2, "Кофе");
        await SeedWare(3, "Чай");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithoutPayment();

        var result = await _repository.Close(1746);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Документ оплачен не полностью"));
    }

    [Test]
    public async Task Close_без_ГП_пишет_только_55()
    {
        await SeedWare(2, "Кофе");
        await SeedWare(3, "Чай");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithoutPayment(printGroupCode: 0, printGroupClose: 0);
        await _repository.AddPayment(1746, PayItems(153.54));

        var result = await _repository.Close(1746);

        Assert.That(result.IsSuccess, Is.True);

        var document = await _dbContext.Documents!.AsNoTracking().SingleAsync(d => d.Id == 1746);
        Assert.That(document.State, Is.EqualTo(DocumentStateEnum.Closed));

        var closeRows = await _dbContext.Transactions!.AsNoTracking()
            .Where(t => t.DocumentId == 1746 && (t.TranzType == TranzTypeEnum.Close || t.TranzType == TranzTypeEnum.CloseByPrintGroup))
            .ToListAsync();

        Assert.That(closeRows, Has.Count.EqualTo(1));
        Assert.That(closeRows[0].TranzType, Is.EqualTo(TranzTypeEnum.Close));
        Assert.That(closeRows[0].Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task Close_с_ГП_пишет_49_и_55()
    {
        await SeedWare(2, "Кофе");
        await SeedPayment(1, "Наличные");
        await SeedDeferredWithPartialPayment();
        await _repository.AddPayment(1753, PayItems(88.13, printGroupCode: 1));

        var result = await _repository.Close(1753);

        Assert.That(result.IsSuccess, Is.True);

        var closeRows = await _dbContext.Transactions!.AsNoTracking()
            .Where(t => t.DocumentId == 1753 && (t.TranzType == TranzTypeEnum.Close || t.TranzType == TranzTypeEnum.CloseByPrintGroup))
            .OrderBy(t => t.Id)
            .ToListAsync();

        Assert.That(closeRows, Has.Count.EqualTo(2));
        Assert.That(closeRows[0].TranzType, Is.EqualTo(TranzTypeEnum.CloseByPrintGroup));
        Assert.That(closeRows[0].PrintGroupClose, Is.EqualTo(1));
        Assert.That(closeRows[1].TranzType, Is.EqualTo(TranzTypeEnum.Close));
        Assert.That(closeRows[1].PrintGroupClose, Is.EqualTo(1));
    }

    private async Task SeedWare(int code, string name)
    {
        _dbContext.Wares!.Add(new SprT
        {
            Id = code,
            Code = code,
            Name = name,
            Mark = name,
            IsWare = 1,
            WareType = 0
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedPrintGroup(int id, int code, string name)
    {
        _dbContext.PrintGroups!.Add(new PrintGroup
        {
            Id = id,
            Code = code,
            Name = name,
            Deleted = 0
        });
        await _dbContext.SaveChangesAsync();
    }

    private static List<DeferredReceiptPaymentItem> PayItems(double summ, int printGroupCode = 0, int paymentCode = 1) =>
    [
        new()
        {
            PaymentCode = paymentCode,
            Summ = summ,
            PrintGroupCode = printGroupCode
        }
    ];

    private async Task SeedPayment(
        int code,
        string name,
        int? printGroupId = 0,
        int? isFiscal = 1,
        int? fiscalOperation = 0,
        int? ecrPayment = 0)
    {
        _dbContext.Payments!.Add(new Payment
        {
            Id = code,
            Code = code,
            Name = name,
            Operation = PaymentOperationEnum.Cash,
            PrintGroupId = printGroupId,
            IsFiscalPayment = isFiscal,
            FiscalOperation = fiscalOperation,
            EcrPayment = ecrPayment,
            Deleted = 0
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedDeferredWithoutPayment(int printGroupCode = 1, int printGroupClose = 1)
    {
        _dbContext.Documents!.Add(Document(1746, 207, DocumentStateEnum.Deffered, 153.54, lastPaymNum: 0, printGroupCode));
        _dbContext.Transactions!.AddRange(
            Open(1747, 1746, 2, 153.54),
            Ware(1748, 1746, wareCode: 2, pos: 1, price: 98.13, printGroupClose),
            Ware(1750, 1746, wareCode: 3, pos: 2, price: 55.41, printGroupClose));
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedDeferredWithPartialPayment()
    {
        _dbContext.Documents!.Add(Document(1753, 208, DocumentStateEnum.Deffered, 98.13, lastPaymNum: 2, printGroupCode: 1));
        _dbContext.Transactions!.AddRange(
            Open(1754, 1753, 1, 98.13),
            Ware(1755, 1753, wareCode: 2, pos: 1, price: 98.13, printGroupClose: 1),
            Pay(1757, 1753, 10, posNumb: 2, printGroupClose: 0, checkNumber: 208),
            PayByGroup(1758, 1753, 10, posNumb: 2, printGroupClose: 1, checkNumber: 208));
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedClosedWithoutPrintGroup()
    {
        _dbContext.Documents!.Add(Document(1787, 215, DocumentStateEnum.Closed, 153.54, lastPaymNum: 2, printGroupCode: 0));
        await _dbContext.SaveChangesAsync();
    }

    private static Document Document(
        long id,
        int checkNumber,
        DocumentStateEnum state,
        double summ,
        int lastPaymNum,
        int printGroupCode) =>
        new()
        {
            Id = id,
            DocumentKindId = 1,
            CheckNumber = checkNumber,
            OpenDate = new DateTime(2026, 9, 3),
            OpenTime = new DateTime(2026, 9, 3, 9, 0, 50),
            OpenUserId = 2151827,
            CloseDate = new DateTime(2026, 9, 3),
            CloseTime = new DateTime(2026, 9, 3, 9, 0, 58),
            CloseUserId = 2151827,
            State = state,
            RmkId = 376,
            Summ = summ,
            SummWd = summ,
            EcrSession = 1,
            ChequeType = ReceiptTypeEnum.Sell,
            OpenRmkId = 376,
            OpenSession = 1,
            PrintGroupCode = printGroupCode,
            LastPaymNum = lastPaymNum,
            OwnerUserId = 2151827,
            IsFiscal = 1
        };

    private static TranzT Open(long id, long documentId, double quantity, double summ) =>
        Base(id, documentId, TranzTypeEnum.OpenDocument, quantity, summ);

    private static TranzT Ware(long id, long documentId, int wareCode, int pos, double price, int printGroupClose)
    {
        var row = Base(id, documentId, TranzTypeEnum.WareFromCatalog, 1, price);
        row.WareCode = wareCode;
        row.Price = price;
        row.PriceWd = price;
        row.SummWd = price;
        row.PosId = pos;
        row.PosNumb = pos;
        row.OrderPos = 1;
        row.PrintGroupClose = printGroupClose;
        return row;
    }

    private static TranzT Pay(long id, long documentId, double summ, int posNumb, int printGroupClose, int checkNumber)
    {
        var row = Base(id, documentId, TranzTypeEnum.Payment, 0, summ);
        row.Price = summ;
        row.SummWd = 0;
        row.Info = 1;
        row.Currency = 1;
        row.PosNumb = posNumb;
        row.CommentCode = 2;
        row.CountFills = 1;
        row.OrderPos = 0;
        row.PrintGroupClose = printGroupClose;
        row.SummRound = summ;
        row.DiscountEnabled = checkNumber;
        return row;
    }

    private static TranzT PayByGroup(long id, long documentId, double summ, int posNumb, int printGroupClose, int checkNumber)
    {
        var row = Pay(id, documentId, summ, posNumb, printGroupClose, checkNumber);
        row.TranzType = TranzTypeEnum.PaymentByPrintGroup;
        return row;
    }

    private static TranzT Base(long id, long documentId, TranzTypeEnum type, double quantity, double summ) =>
        new()
        {
            Id = id,
            DocumentId = documentId,
            TranzDate = new DateTime(2026, 9, 3),
            TranzTime = new DateTime(2026, 9, 3, 9, 0, 50),
            TranzHour = 9,
            TranzType = type,
            Seller = 1,
            Quantity = quantity,
            Summ = summ,
            SummWd = summ,
            TrmkId = 376
        };
}
