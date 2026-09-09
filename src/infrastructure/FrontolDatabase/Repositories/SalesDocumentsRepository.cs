using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Messages.Dto;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class SalesDocumentsRepository : IFrontolSalesDocuments
{
    private readonly ILogger<SalesDocumentsRepository> _logger;
    private readonly MainDbCtx _ctx;

    public SalesDocumentsRepository(ILogger<SalesDocumentsRepository> logger, MainDbCtx ctx)
    {
        _logger = logger;
        _ctx = ctx;
    }

    public async Task<Result<IReadOnlyList<SalesDocument>>> After(long lastDocumentNumber, int limit)
    {
        if (!TablesReady(out var error))
            return Result.Failure<IReadOnlyList<SalesDocument>>(error);

        if (limit <= 0)
            return Result.Success<IReadOnlyList<SalesDocument>>([]);

        try
        {
            var today = DateTime.Today;
            var documents = await _ctx.Documents!
                .AsNoTracking()
                .Where(d =>
                    d.State == DocumentStateEnum.Closed &&
                    d.CloseDate >= today &&
                    d.Id > lastDocumentNumber &&
                    (d.ChequeType == ReceiptTypeEnum.Sell ||
                     d.ChequeType == ReceiptTypeEnum.ReturnSell ||
                     d.ChequeType == ReceiptTypeEnum.SellCorrection))
                .OrderBy(d => d.Id)
                .Take(limit)
                .ToListAsync();

            if (documents.Count == 0)
                return Result.Success<IReadOnlyList<SalesDocument>>([]);

            var ids = documents.Select(d => d.Id).ToList();
            var transactions = await TransactionsByDocumentIdsQuery(ids).ToListAsync();
            var byDocument = transactions
                .GroupBy(t => t.DocumentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var wareCodes = transactions
                .Where(IsPosition)
                .Select(t => t.WareCode)
                .Where(code => code != 0)
                .Distinct()
                .ToList();

            var wares = wareCodes.Count == 0
                ? []
                : await WaresByCodesQuery(wareCodes).ToListAsync();
            var waresByCode = wares
                .GroupBy(w => w.Code)
                .ToDictionary(g => g.Key, g => g.First());

            var cashierIds = documents
                .Select(d => d.CloseUserId)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            var cashiers = cashierIds.Count == 0
                ? []
                : await CashiersByIdsQuery(cashierIds).ToListAsync();
            var cashiersById = cashiers
                .GroupBy(p => p.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var paymentCodes = transactions
                .Where(IsPayment)
                .Select(t => t.Info)
                .Distinct()
                .ToList();

            var paymentKinds = paymentCodes.Count == 0
                ? []
                : await PaymentsByCodesQuery(paymentCodes).ToListAsync();
            var paymentsByCode = paymentKinds
                .GroupBy(p => p.Code)
                .ToDictionary(g => g.Key, g => g.First());

            var docKindIds = documents
                .Select(d => d.DocumentKindId)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            var docKinds = docKindIds.Count == 0
                ? []
                : await DocKindsByIdsQuery(docKindIds).ToListAsync();
            var docKindsById = docKinds
                .GroupBy(k => k.Id)
                .ToDictionary(g => g.Key, g => g.First());

            IReadOnlyList<SalesDocument> result = documents
                .Select(document => Map(
                    document,
                    byDocument.GetValueOrDefault(document.Id) ?? [],
                    waresByCode,
                    cashiersById,
                    paymentsByCode,
                    docKindsById))
                .ToList();

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось прочитать закрытые чеки из GDB");
            return Result.Failure<IReadOnlyList<SalesDocument>>(ex.Message);
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

        if (_ctx.Wares == null)
        {
            error = "Не удалось открыть SPRT";
            return false;
        }

        if (_ctx.Users == null)
        {
            error = "Не удалось открыть USER";
            return false;
        }

        if (_ctx.Payments == null)
        {
            error = "Не удалось открыть PAYMENT";
            return false;
        }

        if (_ctx.DocKinds == null)
        {
            error = "Не удалось открыть DOCKIND";
            return false;
        }

        return true;
    }

    // Firebird 2.1: Contains даёт WHERE FALSE на пустом списке, булева типа нет.
    internal IQueryable<SprT> WaresByCodesQuery(IReadOnlyList<int> wareCodes)
    {
        var parameter = Expression.Parameter(typeof(SprT), "w");
        var property = Expression.Property(parameter, nameof(SprT.Code));
        return _ctx.Wares!
            .AsNoTracking()
            .Where(OrEquals<SprT, int>(parameter, property, wareCodes));
    }

    internal IQueryable<TranzT> TransactionsByDocumentIdsQuery(IReadOnlyList<long> documentIds)
    {
        var parameter = Expression.Parameter(typeof(TranzT), "t");
        var property = Expression.Property(parameter, nameof(TranzT.DocumentId));
        return _ctx.Transactions!
            .AsNoTracking()
            .Where(OrEquals<TranzT, long>(parameter, property, documentIds));
    }

    internal IQueryable<User> CashiersByIdsQuery(IReadOnlyList<int> userIds)
    {
        var parameter = Expression.Parameter(typeof(User), "u");
        var property = Expression.Property(parameter, nameof(User.Id));
        return _ctx.Users!
            .AsNoTracking()
            .Where(OrEquals<User, int>(parameter, property, userIds));
    }

    internal IQueryable<Payment> PaymentsByCodesQuery(IReadOnlyList<int> paymentCodes)
    {
        var parameter = Expression.Parameter(typeof(Payment), "p");
        var property = Expression.Property(parameter, nameof(Payment.Code));
        return _ctx.Payments!
            .AsNoTracking()
            .Where(OrEquals<Payment, int>(parameter, property, paymentCodes));
    }

    internal IQueryable<DocKind> DocKindsByIdsQuery(IReadOnlyList<int> docKindIds)
    {
        var parameter = Expression.Parameter(typeof(DocKind), "k");
        var property = Expression.Property(parameter, nameof(DocKind.Id));
        return _ctx.DocKinds!
            .AsNoTracking()
            .Where(OrEquals<DocKind, int>(parameter, property, docKindIds));
    }

    private static Expression<Func<T, bool>> OrEquals<T, TKey>(
        ParameterExpression parameter,
        MemberExpression property,
        IReadOnlyList<TKey> values)
    {
        Expression body = Expression.Equal(property, Expression.Constant(values[0]));
        for (var i = 1; i < values.Count; i++)
            body = Expression.OrElse(body, Expression.Equal(property, Expression.Constant(values[i])));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static SalesDocument Map(
        Document document,
        List<TranzT> transactions,
        Dictionary<int, SprT> wares,
        Dictionary<int, User> cashiers,
        Dictionary<int, Payment> payments,
        Dictionary<int, DocKind> docKinds)
    {
        var closeDate = document.CloseDate.Year >= 1900 ? document.CloseDate : document.OpenDate;
        cashiers.TryGetValue(document.CloseUserId, out var cashier);
        docKinds.TryGetValue(document.DocumentKindId, out var docKind);

        return new SalesDocument
        {
            DocumentNumber = document.Id,
            DocumentDate = DateOnly.FromDateTime(closeDate),
            DocumentTime = TimeOnly.FromTimeSpan(document.CloseTime.TimeOfDay),
            CheckNumber = document.CheckNumber,
            CashierCode = CashierCode(document, cashier),
            CashierName = cashier?.Name ?? string.Empty,
            Sum = document.Summ,
            DocumentType = docKind?.Code ?? 0,
            Positions = transactions
                .Where(IsPosition)
                .OrderBy(t => t.PosNumb)
                .ThenBy(t => t.Id)
                .Select(t => MapPosition(t, wares))
                .ToList(),
            Payments = MapPayments(transactions, payments)
        };
    }

    private static string CashierCode(Document document, User? cashier)
    {
        if (cashier != null)
            return cashier.Code.ToString();

        return document.CloseUserId == 0 ? string.Empty : document.CloseUserId.ToString();
    }

    private static SalesPosition MapPosition(TranzT transaction, Dictionary<int, SprT> wares)
    {
        wares.TryGetValue(transaction.WareCode, out var catalog);
        var storno = IsStorno(transaction);

        return new SalesPosition
        {
            WareCode = transaction.WareCode,
            Name = catalog?.Name ?? transaction.WareMark,
            Barcode = transaction.Barcode,
            Quantity = storno ? -transaction.Quantity : transaction.Quantity,
            Price = transaction.Price,
            Sum = storno ? -transaction.Summ : transaction.Summ,
            PriceWd = transaction.PriceWd,
            SumWd = storno ? -transaction.SummWd : transaction.SummWd,
            Storno = storno,
            GroupCode = string.Empty,
            GroupName = string.Empty
        };
    }

    private static List<SalesPayment> MapPayments(List<TranzT> transactions, Dictionary<int, Payment> payments)
    {
        var byGroup = transactions
            .Where(t => t.TranzType == TranzTypeEnum.PaymentByPrintGroup)
            .ToList();

        var source = byGroup.Count > 0
            ? byGroup
            : transactions.Where(t => t.TranzType is TranzTypeEnum.Payment or TranzTypeEnum.NonFiscalPayment);

        return source
            .OrderBy(t => t.PosNumb)
            .ThenBy(t => t.Id)
            .Select(t => new SalesPayment
            {
                PaymentCode = t.Info,
                PaymentName = payments.GetValueOrDefault(t.Info)?.Name ?? string.Empty,
                Sum = t.Summ
            })
            .ToList();
    }

    private static bool IsPosition(TranzT transaction) =>
        IsWare(transaction) || IsStorno(transaction);

    private static bool IsPayment(TranzT transaction) =>
        transaction.TranzType is TranzTypeEnum.Payment
            or TranzTypeEnum.PaymentByPrintGroup
            or TranzTypeEnum.NonFiscalPayment;

    private static bool IsWare(TranzT transaction) =>
        transaction.TranzType is TranzTypeEnum.WareFromCatalog or TranzTypeEnum.WareFreePrice;

    // В TRANZT сторно хранит положительные Quantity/Summ/SummWd — для аналитики инвертируем знак.
    private static bool IsStorno(TranzT transaction) =>
        transaction.TranzType is TranzTypeEnum.StornoFromCatalog or TranzTypeEnum.StornoFreePrice;
}
