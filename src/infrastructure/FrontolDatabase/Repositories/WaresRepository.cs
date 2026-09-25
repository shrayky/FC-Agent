using System.Linq.Expressions;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Wares;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class WaresRepository : IFrontolWares
{
    private static readonly DateTime EmptyActDateTime = new(1899, 12, 30);

    private readonly ILogger<WaresRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly IFrontolMainDb _mainDb;

    public WaresRepository(ILogger<WaresRepository> logger, MainDbCtx ctx, IFrontolMainDb mainDb)
    {
        _logger = logger;
        _ctx = ctx;
        _mainDb = mainDb;
    }

    public async Task<Result> Apply(IReadOnlyList<WareGroup> groups, IReadOnlyList<WareItem> wares)
    {
        try
        {
            if (_ctx.Wares == null)
                return Result.Failure("Не удалось открыть SPRT");

            var bdoCode = await BdoCode();
            var groupByCode = await LoadGroups(groups, wares);
            var wareByCode = await LoadWares(wares);
            var taxByCode = await LoadTaxGroups(groups, wares);
            var printByCode = await LoadPrintGroups(groups, wares);

            foreach (var group in groups)
            {
                var code = ParseCode(group.GroupCode);
                if (code == 0)
                    continue;

                var parent = FindGroup(groupByCode, ParseCode(group.ParentCode));
                var row = await UpsertSprT(
                    groupByCode.GetValueOrDefault(code),
                    code,
                    isWare: false,
                    group.Name,
                    parent,
                    group.Extra,
                    bdoCode,
                    taxByCode,
                    printByCode);
                groupByCode[code] = row;
            }

            foreach (var ware in wares)
            {
                if (ware.WareCode == 0)
                    continue;

                var parent = FindGroup(groupByCode, ParseCode(ware.GroupCode));
                var row = await UpsertSprT(
                    wareByCode.GetValueOrDefault(ware.WareCode),
                    ware.WareCode,
                    isWare: true,
                    ware.Name,
                    parent,
                    ware.Extra,
                    bdoCode,
                    taxByCode,
                    printByCode);
                wareByCode[ware.WareCode] = row;
                await ApplyBarcodes(row.Id, ware.Extra, bdoCode);
                await ApplyRemainAndPrice(row.Id, ware.Extra, bdoCode);
            }

            await _ctx.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка записи справочника в Frontol");
            return Result.Failure(ex.Message);
        }
    }

    private async Task<SprT> UpsertSprT(
        SprT? existing,
        int code,
        bool isWare,
        string name,
        SprT? parent,
        JsonElement extra,
        int bdoCode,
        Dictionary<int, int> taxByCode,
        Dictionary<int, int> printByCode)
    {
        SprT row;
        if (existing == null)
        {
            var id = await _mainDb.NextChangeId();
            row = new SprT
            {
                Id = id,
                Code = code,
                IsWare = isWare ? 1 : 0,
                InsertChange = id,
                ChangeCount = id,
                DatabaseCode = bdoCode
            };
            _ctx.Wares!.Add(row);
        }
        else
        {
            row = existing;
            row.ChangeCount = await _mainDb.NextChangeId();
        }

        row.Name = name;
        row.Mark = ExtraString(extra, "mark", row.Mark);
        row.ParentId = parent?.Id ?? 0;
        row.HierLevel = parent == null ? 0 : (parent.HierLevel ?? 0) + 1;
        row.Flags = ExtraInt(extra, "flags", isWare ? 6 : 0);
        row.WareType = ExtraInt(extra, "wareType", 0);
        row.Measure = ExtraInt(extra, "measure", 0);
        row.ItemType = ExtraInt(extra, "itemType", 0);
        row.TaxGroupId = ResolveId(taxByCode, ExtraInt(extra, "taxGroupCode", 0));
        row.PrintGroupClose = ResolveId(printByCode, ExtraInt(extra, "printGroupCloseCode", 0));
        row.Deleted = 0;
        row.DatabaseCode = bdoCode;
        return row;
    }

    // Frontol: перед вставкой удаляем BARCODE с тем же значением, иначе касса держит чужой WareID.
    private async Task ApplyBarcodes(int wareId, JsonElement extra, int bdoCode)
    {
        if (_ctx.BarCodes == null || extra.ValueKind != JsonValueKind.Object)
            return;
        if (!extra.TryGetProperty("barcodes", out var items) || items.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in items.EnumerateArray())
        {
            var value = ExtraString(item, "barcode", string.Empty);
            if (value == string.Empty)
                continue;

            var existing = await _ctx.BarCodes.Where(b => b.Barcode == value).ToListAsync();
            _ctx.BarCodes.RemoveRange(existing);

            var id = await _mainDb.NextChangeId();
            _ctx.BarCodes.Add(new BarCode
            {
                Id = id,
                WareId = wareId,
                Barcode = value,
                Factor = ExtraDouble(item, "factor", 1),
                ChangeCount = id,
                InsertChange = id,
                DatabaseCode = bdoCode
            });
        }
    }

    // Frontol: количества нет в Remain; начальный остаток — RemainD с DType=1. Цена всегда через RemainID.
    private async Task ApplyRemainAndPrice(int wareId, JsonElement extra, int bdoCode)
    {
        var loadRemain = ExtraBool(extra, "loadRemain");
        var hasPrice = HasNumber(extra, "price");
        if (!loadRemain && !hasPrice)
            return;
        if (_ctx.Remains == null)
            return;

        var remain = await _ctx.Remains.FirstOrDefaultAsync(row =>
            row.WareId == wareId
            && row.AspectValue1Id == 0
            && row.AspectValue2Id == 0
            && row.AspectValue3Id == 0
            && row.AspectValue4Id == 0
            && row.AspectValue5Id == 0);

        if (remain == null)
        {
            var id = await _mainDb.NextChangeId();
            remain = new Remain
            {
                Id = id,
                WareId = wareId,
                InsertChange = id,
                ChangeCount = id,
                DatabaseCode = bdoCode
            };
            _ctx.Remains.Add(remain);
        }
        else
        {
            remain.ChangeCount = await _mainDb.NextChangeId();
        }

        remain.UseRemain = loadRemain ? 1 : 0;
        remain.Deleted = 0;
        remain.DatabaseCode = bdoCode;

        if (loadRemain && _ctx.RemainDs != null)
        {
            var delta = ExtraDouble(extra, "remainQuantity", 0);
            var remainD = await _ctx.RemainDs.FirstOrDefaultAsync(row =>
                row.RemainId == remain.Id && row.DType == 1);
            if (remainD == null)
            {
                var id = await _mainDb.NextChangeId();
                remainD = new RemainD
                {
                    Id = id,
                    RemainId = remain.Id,
                    DType = 1,
                    DocumentId = 0,
                    ChangeCount = id,
                    DatabaseCode = bdoCode
                };
                _ctx.RemainDs.Add(remainD);
            }
            else
            {
                remainD.ChangeCount = await _mainDb.NextChangeId();
            }

            remainD.Delta = delta;
        }

        if (hasPrice && _ctx.PriceDatas != null)
        {
            var act = ActDateTime(extra);
            var price = ExtraDouble(extra, "price", 0);
            var priceRow = await _ctx.PriceDatas.FirstOrDefaultAsync(row =>
                row.RemainId == remain.Id && row.ActDateTime == act);
            if (priceRow == null)
            {
                var id = await _mainDb.NextChangeId();
                priceRow = new PriceData
                {
                    Id = id,
                    RemainId = remain.Id,
                    ActDateTime = act,
                    InsertChange = id,
                    ChangeCount = id,
                    DatabaseCode = bdoCode
                };
                _ctx.PriceDatas.Add(priceRow);
            }
            else
            {
                priceRow.ChangeCount = await _mainDb.NextChangeId();
            }

            priceRow.Price = price;
        }
    }

    private async Task<Dictionary<int, SprT>> LoadGroups(
        IReadOnlyList<WareGroup> groups,
        IReadOnlyList<WareItem> wares)
    {
        var codes = groups
            .Select(group => ParseCode(group.GroupCode))
            .Concat(groups.Select(group => ParseCode(group.ParentCode)))
            .Concat(wares.Select(ware => ParseCode(ware.GroupCode)))
            .Where(code => code != 0)
            .Distinct()
            .ToList();
        if (codes.Count == 0)
            return [];

        var rows = await QueryByCodes(codes, isWare: 0).ToListAsync();
        return rows.ToDictionary(row => row.Code);
    }

    private async Task<Dictionary<int, SprT>> LoadWares(IReadOnlyList<WareItem> wares)
    {
        var codes = wares.Select(ware => ware.WareCode).Where(code => code != 0).Distinct().ToList();
        if (codes.Count == 0)
            return [];

        var rows = await QueryByCodes(codes, isWare: 1).ToListAsync();
        return rows.ToDictionary(row => row.Code);
    }

    private IQueryable<SprT> QueryByCodes(IReadOnlyList<int> codes, int isWare)
    {
        var parameter = Expression.Parameter(typeof(SprT), "w");
        var property = Expression.Property(parameter, nameof(SprT.Code));
        return _ctx.Wares!
            .Where(ware => ware.IsWare == isWare)
            .Where(OrEquals<SprT, int>(parameter, property, codes));
    }

    private async Task<Dictionary<int, int>> LoadTaxGroups(
        IReadOnlyList<WareGroup> groups,
        IReadOnlyList<WareItem> wares)
    {
        if (_ctx.TaxGroups == null)
            return [];

        var codes = ExtraCodes(groups, wares, "taxGroupCode");
        if (codes.Count == 0)
            return [];

        var parameter = Expression.Parameter(typeof(TaxGroup), "t");
        var property = Expression.Property(parameter, nameof(TaxGroup.Code));
        var rows = await _ctx.TaxGroups
            .Where(OrEquals<TaxGroup, int>(parameter, property, codes))
            .ToListAsync();
        return rows.ToDictionary(row => row.Code, row => row.Id);
    }

    private async Task<Dictionary<int, int>> LoadPrintGroups(
        IReadOnlyList<WareGroup> groups,
        IReadOnlyList<WareItem> wares)
    {
        if (_ctx.PrintGroups == null)
            return [];

        var codes = ExtraCodes(groups, wares, "printGroupCloseCode");
        if (codes.Count == 0)
            return [];

        var parameter = Expression.Parameter(typeof(PrintGroup), "g");
        var property = Expression.Property(parameter, nameof(PrintGroup.Code));
        var rows = await _ctx.PrintGroups
            .Where(OrEquals<PrintGroup, int>(parameter, property, codes))
            .ToListAsync();
        return rows.ToDictionary(row => row.Code, row => row.Id);
    }

    private async Task<int> BdoCode()
    {
        if (_ctx.CustomDb == null)
            return 0;

        var row = await _ctx.CustomDb.AsNoTracking().FirstOrDefaultAsync();
        return row?.Code ?? 0;
    }

    private static SprT? FindGroup(Dictionary<int, SprT> groupByCode, int code) =>
        code == 0 ? null : groupByCode.GetValueOrDefault(code);

    private static int ResolveId(Dictionary<int, int> byCode, int code) =>
        code != 0 && byCode.TryGetValue(code, out var id) ? id : 0;

    private static List<int> ExtraCodes(
        IReadOnlyList<WareGroup> groups,
        IReadOnlyList<WareItem> wares,
        string name)
    {
        return groups.Select(group => ExtraInt(group.Extra, name, 0))
            .Concat(wares.Select(ware => ExtraInt(ware.Extra, name, 0)))
            .Where(code => code != 0)
            .Distinct()
            .ToList();
    }

    private static int ParseCode(string value) =>
        int.TryParse(value, out var code) ? code : 0;

    private static int ExtraInt(JsonElement extra, string name, int fallback)
    {
        if (!TryProperty(extra, name, out var value))
            return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : fallback;
    }

    private static double ExtraDouble(JsonElement extra, string name, double fallback)
    {
        if (!TryProperty(extra, name, out var value))
            return fallback;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : fallback;
    }

    private static bool ExtraBool(JsonElement extra, string name) =>
        TryProperty(extra, name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static string ExtraString(JsonElement extra, string name, string fallback)
    {
        if (!TryProperty(extra, name, out var value) || value.ValueKind != JsonValueKind.String)
            return fallback;
        return value.GetString() ?? fallback;
    }

    private static bool HasNumber(JsonElement extra, string name) =>
        TryProperty(extra, name, out var value) && value.ValueKind == JsonValueKind.Number;

    // Frontol: пустой ActDateTime записывается как 1899-12-30.
    private static DateTime ActDateTime(JsonElement extra)
    {
        if (!TryProperty(extra, name: "priceActDateTime", out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return EmptyActDateTime;
        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var parsed))
            return parsed;
        return EmptyActDateTime;
    }

    private static bool TryProperty(JsonElement extra, string name, out JsonElement value)
    {
        value = default;
        return extra.ValueKind == JsonValueKind.Object && extra.TryGetProperty(name, out value);
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
}
