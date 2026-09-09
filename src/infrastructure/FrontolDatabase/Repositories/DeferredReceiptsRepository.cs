using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Receipts;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class DeferredReceiptsRepository : IFrontolDeferredReceipts
{
    private const int DefaultCurrency = 1;
    private const int PaymentCommentCode = 2;

    private readonly ILogger<DeferredReceiptsRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly IFrontolMainDb _mainDb;

    public DeferredReceiptsRepository(
        ILogger<DeferredReceiptsRepository> logger,
        MainDbCtx ctx,
        IFrontolMainDb mainDb)
    {
        _logger = logger;
        _ctx = ctx;
        _mainDb = mainDb;
    }

    public async Task<Result<ReceiptList>> List()
    {
        if (!TablesReady(out var error))
            return Result.Failure<ReceiptList>(error);

        try
        {
            var documents = (await _ctx.Documents!
                .AsNoTracking()
                .Where(d => d.State == DocumentStateEnum.Deffered)
                .ToListAsync())
                .OrderByDescending(d => d.OpenDate.Date + d.OpenTime.TimeOfDay)
                .ThenByDescending(d => d.Id)
                .ToList();

            var receipts = new List<Receipt>();
            foreach (var document in documents)
            {
                var mapped = await MapReceipt(document);
                if (mapped.IsFailure)
                    return Result.Failure<ReceiptList>(mapped.Error);

                receipts.Add(mapped.Value);
            }

            var printGroups = await LoadPrintGroups();
            return Result.Success(new ReceiptList
            {
                Receipts = receipts,
                PaymentKinds = await LoadPaymentKinds(printGroups),
                PrintGroups = printGroups
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения отложенных чеков");
            return Result.Failure<ReceiptList>(ex.Message);
        }
    }

    public async Task<Result<int>> Count()
    {
        if (_ctx.Documents == null)
            return Result.Failure<int>("Не удалось открыть Documents");

        try
        {
            var count = await _ctx.Documents
                .AsNoTracking()
                .CountAsync(d => d.State == DocumentStateEnum.Deffered);

            return Result.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подсчёта отложенных чеков");
            return Result.Failure<int>(ex.Message);
        }
    }

    public async Task<Result<Receipt>> Cancel(long documentId)
    {
        if (!TablesReady(out var error))
            return Result.Failure<Receipt>(error);

        try
        {
            var loaded = await LoadDeferred(documentId);
            if (loaded.IsFailure)
                return Result.Failure<Receipt>(loaded.Error);

            var (document, transactions) = loaded.Value;
            var now = DateTime.Now;
            var seller = SellerOf(transactions);
            var wareCount = WareCount(transactions);

            _ctx.Transactions!.Add(CreateCloseLike(
                await NextId(),
                document,
                TranzTypeEnum.Cancel,
                wareCount,
                printGroupClose: document.PrintGroupCode,
                seller,
                now,
                openCountFills: CountFillsOfOpen(transactions)));

            document.State = DocumentStateEnum.Canceled;
            document.CloseDate = now.Date;
            document.CloseTime = now;
            document.CloseUserId = document.OpenUserId;

            await _ctx.SaveChangesAsync();
            return await MapReceipt(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отмены отложенного чека {DocumentId}", documentId);
            return Result.Failure<Receipt>(ex.Message);
        }
    }

    public async Task<Result<Receipt>> Close(
        long documentId,
        IReadOnlyList<ReceiptPaymentItem>? payments = null)
    {
        if (!TablesReady(out var error))
            return Result.Failure<Receipt>(error);

        try
        {
            var loaded = await LoadDeferred(documentId);
            if (loaded.IsFailure)
                return Result.Failure<Receipt>(loaded.Error);

            var (document, transactions) = loaded.Value;

            if (payments is { Count: > 0 })
            {
                var added = await WritePayments(document, transactions, payments);
                if (added.IsFailure)
                    return Result.Failure<Receipt>(added.Error);

                transactions.AddRange(_ctx.ChangeTracker.Entries<TranzT>()
                    .Where(e => e.State == EntityState.Added && e.Entity.DocumentId == documentId)
                    .Select(e => e.Entity));
            }

            if (RemainSumm(document, transactions) > 0.001)
                return Result.Failure<Receipt>("Документ оплачен не полностью");

            var now = DateTime.Now;
            var seller = SellerOf(transactions);
            var wareCount = WareCount(transactions);
            var printGroups = WarePrintGroupCodes(transactions);

            foreach (var printGroup in printGroups)
            {
                _ctx.Transactions!.Add(CreateCloseLike(
                    await NextId(),
                    document,
                    TranzTypeEnum.CloseByPrintGroup,
                    quantity: 0,
                    printGroupClose: printGroup,
                    seller,
                    now,
                    openCountFills: 0));
            }

            _ctx.Transactions!.Add(CreateCloseLike(
                await NextId(),
                document,
                TranzTypeEnum.Close,
                wareCount,
                printGroupClose: document.PrintGroupCode,
                seller,
                now,
                openCountFills: CountFillsOfOpen(transactions)));

            document.State = DocumentStateEnum.Closed;
            document.CloseDate = now.Date;
            document.CloseTime = now;
            document.CloseUserId = document.OpenUserId;

            await _ctx.SaveChangesAsync();
            return await MapReceipt(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка закрытия отложенного чека {DocumentId}", documentId);
            return Result.Failure<Receipt>(ex.Message);
        }
    }

    public async Task<Result<Receipt>> AddPayment(
        long documentId,
        IReadOnlyList<ReceiptPaymentItem> payments)
    {
        if (!TablesReady(out var error))
            return Result.Failure<Receipt>(error);

        try
        {
            var loaded = await LoadDeferred(documentId);
            if (loaded.IsFailure)
                return Result.Failure<Receipt>(loaded.Error);

            var (document, transactions) = loaded.Value;
            var added = await WritePayments(document, transactions, payments);
            if (added.IsFailure)
                return Result.Failure<Receipt>(added.Error);

            await _ctx.SaveChangesAsync();
            return await MapReceipt(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка оплаты отложенного чека {DocumentId}", documentId);
            return Result.Failure<Receipt>(ex.Message);
        }
    }

    private bool TablesReady(out string error)
    {
        error = string.Empty;

        if (_ctx.Documents == null)
        {
            error = "Не удалось открыть Documents";
            return false;
        }

        if (_ctx.Transactions == null)
        {
            error = "Не удалось открыть TRANZT";
            return false;
        }

        if (_ctx.Payments == null)
        {
            error = "Не удалось открыть PAYMENT";
            return false;
        }

        if (_ctx.Wares == null)
        {
            error = "Не удалось открыть SPRT";
            return false;
        }

        if (_ctx.PrintGroups == null)
        {
            error = "Не удалось открыть PRINTGROUP";
            return false;
        }

        return true;
    }

    private async Task<Result> WritePayments(
        Document document,
        List<TranzT> transactions,
        IReadOnlyList<ReceiptPaymentItem> payments)
    {
        if (payments.Count == 0)
            return Result.Failure("Нет оплат для записи");

        if (payments.Any(p => p.Summ <= 0))
            return Result.Failure("Сумма оплаты должна быть больше нуля");

        var total = payments.Sum(p => p.Summ);
        if (total - RemainSumm(document, transactions) > 0.001)
            return Result.Failure("Сумма оплаты больше остатка");

        var kinds = await _ctx.Payments!.AsNoTracking().Where(p => p.Deleted == 0).ToListAsync();
        var groups = await LoadPrintGroups();

        foreach (var item in payments)
        {
            var kind = kinds.FirstOrDefault(p => p.Code == item.PaymentCode);
            if (kind is null)
                return Result.Failure("Вид оплаты не найден");

            if (!PaymentFitsPrintGroup(kind, item.PrintGroupCode, groups))
                return Result.Failure("Вид оплаты не подходит для группы печати");

            if (item.PrintGroupCode != 0)
            {
                var remainGroup = RemainByPrintGroup(transactions, item.PrintGroupCode);
                var alreadyInBatch = payments
                    .Where(p => p.PrintGroupCode == item.PrintGroupCode)
                    .Sum(p => p.Summ);
                if (alreadyInBatch - remainGroup > 0.001)
                    return Result.Failure("Сумма оплаты больше остатка по группе печати");
            }
        }

        var now = DateTime.Now;
        var seller = SellerOf(transactions);
        var posNumb = NextPaymentNumber(document.LastPaymNum);

        foreach (var item in payments)
        {
            var kind = kinds.First(p => p.Code == item.PaymentCode);

            _ctx.Transactions!.Add(CreatePayment(
                await NextId(),
                document,
                TranzTypeEnum.Payment,
                kind,
                item.Summ,
                posNumb,
                printGroupClose: 0,
                seller,
                now));

            if (item.PrintGroupCode != 0)
            {
                _ctx.Transactions!.Add(CreatePayment(
                    await NextId(),
                    document,
                    TranzTypeEnum.PaymentByPrintGroup,
                    kind,
                    item.Summ,
                    posNumb,
                    printGroupClose: item.PrintGroupCode,
                    seller,
                    now));
            }

            posNumb++;
        }

        document.LastPaymNum = posNumb - 1;
        return Result.Success();
    }

    private async Task<Result<(Document Document, List<TranzT> Transactions)>> LoadDeferred(long documentId)
    {
        var document = await _ctx.Documents!.SingleOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
            return Result.Failure<(Document, List<TranzT>)>("Документ не найден");

        if (document.State != DocumentStateEnum.Deffered)
            return Result.Failure<(Document, List<TranzT>)>("Документ не отложен");

        var transactions = await _ctx.Transactions!
            .Where(t => t.DocumentId == documentId)
            .ToListAsync();

        return Result.Success((document, transactions));
    }

    private async Task<Result<Receipt>> MapReceipt(Document document)
    {
        var transactions = await _ctx.Transactions!
            .AsNoTracking()
            .Where(t => t.DocumentId == document.Id)
            .ToListAsync();

        var wareCodes = transactions
            .Where(IsWare)
            .Select(t => t.WareCode)
            .Distinct()
            .ToList();

        var wares = await LoadWares(wareCodes);

        var payments = await _ctx.Payments!
            .AsNoTracking()
            .ToListAsync();

        var paid = PaidSumm(transactions);
        return Result.Success(new Receipt
        {
            Id = document.Id,
            CheckNumber = document.CheckNumber,
            OpenDate = document.OpenDate,
            OpenTime = document.OpenDate.Date + document.OpenTime.TimeOfDay,
            Summ = document.Summ,
            SummWd = document.SummWd,
            PaidSumm = paid,
            RemainSumm = Math.Max(0, document.SummWd - paid),
            HasPrintGroup = HasPrintGroup(document, transactions),
            Positions = MapPositions(transactions, wares),
            Payments = MapPayments(transactions, payments)
        });
    }

    private async Task<List<SprT>> LoadWares(IReadOnlyList<int> wareCodes)
    {
        if (wareCodes.Count == 0)
            return [];

        return await WaresByCodesQuery(wareCodes).ToListAsync();
    }

    // Firebird 2.1: Contains даёт WHERE FALSE на пустом списке, булева типа нет.
    internal IQueryable<SprT> WaresByCodesQuery(IReadOnlyList<int> wareCodes)
    {
        var parameter = Expression.Parameter(typeof(SprT), "w");
        var property = Expression.Property(parameter, nameof(SprT.Code));
        Expression body = Expression.Equal(property, Expression.Constant(wareCodes[0]));
        for (var i = 1; i < wareCodes.Count; i++)
            body = Expression.OrElse(body, Expression.Equal(property, Expression.Constant(wareCodes[i])));

        return _ctx.Wares!
            .AsNoTracking()
            .Where(Expression.Lambda<Func<SprT, bool>>(body, parameter));
    }

    private async Task<List<PrintGroupInfo>> LoadPrintGroups()
    {
        return await _ctx.PrintGroups!
            .AsNoTracking()
            .Where(g => g.Deleted == 0)
            .Select(g => new PrintGroupInfo
            {
                Id = g.Id,
                Code = g.Code,
                Name = g.Name
            })
            .ToListAsync();
    }

    private async Task<List<PaymentKind>> LoadPaymentKinds(List<PrintGroupInfo> printGroups)
    {
        var kinds = await _ctx.Payments!
            .AsNoTracking()
            .Where(p => p.Deleted == 0)
            .ToListAsync();

        return kinds.Select(p => new PaymentKind
        {
            Code = p.Code,
            Name = p.Name,
            Operation = p.Operation,
            PrintGroupId = p.PrintGroupId ?? 0,
            PrintGroupCode = printGroups.FirstOrDefault(g => g.Id == (p.PrintGroupId ?? 0))?.Code ?? 0
        }).ToList();
    }

    private async Task<long> NextId() => await _mainDb.NextChangeId();

    private TranzT CreatePayment(
        long id,
        Document document,
        TranzTypeEnum type,
        Payment payment,
        double summ,
        int posNumb,
        int printGroupClose,
        int seller,
        DateTime now) =>
        new()
        {
            Id = id,
            DocumentId = document.Id,
            TranzDate = now.Date,
            TranzTime = now,
            TranzHour = now.Hour,
            TranzType = type,
            Seller = seller,
            Price = summ,
            Quantity = 0,
            Summ = summ,
            PriceWd = 0,
            SummWd = 0,
            Info = payment.Code,
            Currency = DefaultCurrency,
            PosNumb = posNumb,
            CommentCode = PaymentCommentCode,
            CountFills = payment.IsFiscalPayment ?? 0,
            OrderPos = (int)payment.Operation,
            TrmkId = document.RmkId,
            PrintGroupClose = printGroupClose,
            SummRound = summ,
            DiscountEnabled = document.CheckNumber
        };

    private static TranzT CreateCloseLike(
        long id,
        Document document,
        TranzTypeEnum type,
        double quantity,
        int printGroupClose,
        int seller,
        DateTime now,
        double openCountFills) =>
        new()
        {
            Id = id,
            DocumentId = document.Id,
            TranzDate = now.Date,
            TranzTime = now,
            TranzHour = now.Hour,
            TranzType = type,
            Seller = seller,
            Quantity = quantity,
            Summ = document.Summ,
            SummWd = document.SummWd,
            CountFills = openCountFills,
            TrmkId = document.RmkId,
            PrintGroupClose = printGroupClose
        };

    private static List<ReceiptPosition> MapPositions(List<TranzT> transactions, List<SprT> wares)
    {
        var added = transactions.Where(IsWare).ToList();
        var storno = transactions.Where(IsStorno).ToList();

        var positions = new List<ReceiptPosition>();
        foreach (var ware in added)
        {
            // Frontol: сторно (тип 12) пишет Quantity отрицательным.
            var cancelled = storno
                .Where(s => s.PosId == ware.PosId && s.WareCode == ware.WareCode)
                .Sum(s => Math.Abs(s.Quantity));

            var remaining = ware.Quantity - cancelled;
            var stornoed = remaining <= 0.000001;
            var quantity = stornoed ? ware.Quantity : remaining;

            var catalog = wares.FirstOrDefault(w => w.Code == ware.WareCode);
            positions.Add(new ReceiptPosition
            {
                WareCode = ware.WareCode,
                Name = catalog?.Name ?? ware.WareMark,
                Mark = string.IsNullOrEmpty(ware.WareMark) ? catalog?.Mark ?? string.Empty : ware.WareMark,
                Barcode = ware.Barcode,
                Quantity = quantity,
                Price = ware.Price,
                Summ = stornoed ? ware.Summ : ware.Summ * (quantity / ware.Quantity),
                WareType = catalog?.WareType ?? 0,
                PrintGroupCode = ware.PrintGroupClose,
                Storno = stornoed
            });
        }

        return positions;
    }

    private static List<ReceiptPayment> MapPayments(List<TranzT> transactions, List<Payment> payments)
    {
        var byGroup = transactions
            .Where(t => t.TranzType == TranzTypeEnum.PaymentByPrintGroup)
            .ToList();

        var source = byGroup.Count > 0
            ? byGroup
            : transactions.Where(t => t.TranzType is TranzTypeEnum.Payment or TranzTypeEnum.NonFiscalPayment);

        return source
            .Select(t => new ReceiptPayment
            {
                PaymentCode = t.Info,
                PaymentName = payments.FirstOrDefault(p => p.Code == t.Info)?.Name ?? string.Empty,
                Summ = t.Summ,
                PrintGroupCode = t.PrintGroupClose
            })
            .ToList();
    }

    private static bool IsWare(TranzT transaction) =>
        transaction.TranzType is TranzTypeEnum.WareFromCatalog or TranzTypeEnum.WareFreePrice;

    private static bool IsStorno(TranzT transaction) =>
        transaction.TranzType is TranzTypeEnum.StornoFromCatalog or TranzTypeEnum.StornoFreePrice;

    private static bool HasPrintGroup(Document document, IEnumerable<TranzT> transactions) =>
        WarePrintGroupCodes(transactions).Count > 0;

    private static List<int> WarePrintGroupCodes(IEnumerable<TranzT> transactions) =>
        transactions
            .Where(t => IsWare(t) && t.PrintGroupClose != 0)
            .Select(t => t.PrintGroupClose)
            .Distinct()
            .ToList();

    private static bool PaymentFitsPrintGroup(
        Payment kind,
        int printGroupCode,
        List<PrintGroupInfo> groups)
    {
        if ((kind.PrintGroupId ?? 0) == 0)
            return true;

        var kindCode = groups.FirstOrDefault(g => g.Id == kind.PrintGroupId)?.Code ?? 0;
        return kindCode == 0 || kindCode == printGroupCode;
    }

    private static double RemainByPrintGroup(IEnumerable<TranzT> transactions, int printGroupCode)
    {
        var goods = transactions
            .Where(t => IsWare(t) && t.PrintGroupClose == printGroupCode)
            .Sum(t => t.SummWd);

        var paid = transactions
            .Where(t => t.TranzType == TranzTypeEnum.PaymentByPrintGroup && t.PrintGroupClose == printGroupCode)
            .Sum(t => t.Summ);

        return goods - paid;
    }

    private static double PaidSumm(IEnumerable<TranzT> transactions) =>
        transactions
            .Where(t => t.TranzType is TranzTypeEnum.Payment or TranzTypeEnum.NonFiscalPayment)
            .Sum(t => t.Summ);

    private static double RemainSumm(Document document, IEnumerable<TranzT> transactions) =>
        document.SummWd - PaidSumm(transactions);

    private static int WareCount(IEnumerable<TranzT> transactions) =>
        transactions.Count(IsWare) - transactions.Count(IsStorno);

    private static int SellerOf(IEnumerable<TranzT> transactions) =>
        transactions.FirstOrDefault(t => t.TranzType == TranzTypeEnum.OpenDocument)?.Seller ?? 1;

    private static double CountFillsOfOpen(IEnumerable<TranzT> transactions) =>
        transactions.FirstOrDefault(t => t.TranzType == TranzTypeEnum.OpenDocument)?.CountFills ?? 0;

    // Frontol: первая оплата получает PosNumb = 2, не 1.
    private static int NextPaymentNumber(int lastPaymNum) =>
        lastPaymNum == 0 ? 2 : lastPaymNum + 1;
}
