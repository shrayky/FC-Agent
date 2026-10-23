using CSharpFunctionalExtensions;
using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class DocumentConfigurationRepository : IFrontolDocumentConfiguration
{
    private readonly ILogger<DocumentConfigurationRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly IFrontolMainDb _mainDb;

    public DocumentConfigurationRepository(
        ILogger<DocumentConfigurationRepository> logger,
        MainDbCtx ctx,
        IFrontolMainDb mainDb)
    {
        _logger = logger;
        _ctx = ctx;
        _mainDb = mainDb;
    }

    public async Task<Result<DocumentConfiguration>> Read()
    {
        try
        {
            return Result.Success(new DocumentConfiguration
            {
                DocumentKinds = await ReadKinds(),
                DocumentTemplates = await ReadTemplates(),
                PrintGroups = await ReadPrintGroups(),
                Comments = await ReadComments()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения настроек документов Frontol");
            return Result.Failure<DocumentConfiguration>(ex.Message);
        }
    }

    // Секции идут от справочника к подчинённой строке. Список null таблицу не меняет,
    // пустой список удаляет из неё строки. Удалённый вид или шаблон забирает свои строки DOCKINDTMPL.
    public async Task<Result> Apply(DocumentConfiguration configuration)
    {
        if (IsUntouched(configuration))
            return Result.Success();

        var missing = MissingTable(configuration);
        if (missing != null)
            return Result.Failure(missing);

        var tooLong = TooLong(configuration);
        if (tooLong != null)
            return Result.Failure(tooLong);

        try
        {
            if (configuration.Comments != null)
                await ApplyComments(configuration.Comments);

            if (configuration.PrintGroups != null)
                await ApplyPrintGroups(configuration.PrintGroups);

            if (configuration.DocumentTemplates != null)
                await ApplyTemplates(configuration.DocumentTemplates);

            if (configuration.DocumentKinds != null)
                await ApplyKinds(configuration.DocumentKinds);

            await _ctx.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка записи настроек документов Frontol");
            return Result.Failure(ex.Message);
        }
    }

    // Ограничение Frontol: не каждая операция печатает каждым действием.
    internal static bool ActAllowed(DocOperationEnum operation, DocOpeartionActEnum act)
    {
        if (!Enum.IsDefined(operation) || !Enum.IsDefined(act))
            return false;

        return operation switch
        {
            DocOperationEnum.Close => act is DocOpeartionActEnum.CloseDocument
                or DocOpeartionActEnum.PrintCopy
                or DocOpeartionActEnum.PrintMark,
            DocOperationEnum.Copy => act == DocOpeartionActEnum.PrintCopy,
            DocOperationEnum.Mark => act == DocOpeartionActEnum.PrintMark,
            DocOperationEnum.Postpone => act is DocOpeartionActEnum.PrintCopy or DocOpeartionActEnum.PrintMark,
            DocOperationEnum.Cancel => act is DocOpeartionActEnum.PrintCopy or DocOpeartionActEnum.PrintMark,
            DocOperationEnum.Bill => act == DocOpeartionActEnum.PrintBill,
            DocOperationEnum.Reserve => act == DocOpeartionActEnum.PrintCopy,
            _ => false
        };
    }

    private async Task<List<DocumentKind>?> ReadKinds()
    {
        if (_ctx.DocKinds == null)
            return null;

        var rows = await _ctx.DocKinds.AsNoTracking().ToListAsync();
        var links = await ReadLinksByKind();
        return rows.Select(row => new DocumentKind
        {
            Code = row.Code,
            Name = row.Name,
            Text = row.Text,
            Identif = row.DocumentTypeIdentificator,
            ReceiptType = row.ReceiptType,
            AskComment = row.AskComment,
            AskEmployee = row.AskEmployee,
            CopyEmployee = row.CopyEmployee,
            AskCommentOnOpen = row.AskCommentOnOpen,
            Recompense = row.Recompense,
            DocumentKindTemplates = links == null
                ? null
                : links.GetValueOrDefault(row.Id) ?? []
        }).ToList();
    }

    private async Task<List<DocumentTemplate>?> ReadTemplates()
    {
        if (_ctx.DocumentTemplates == null)
            return null;

        var rows = await _ctx.DocumentTemplates.AsNoTracking().ToListAsync();
        var byId = rows.ToDictionary(row => row.Id);

        return rows.Select(row => new DocumentTemplate
        {
            Code = row.Code,
            Name = row.Name ?? string.Empty,
            IsFolder = row.IsFolder == 1,
            ParentCode = (row.ParentID ?? 0) == 0 ? 0 : byId.GetValueOrDefault(row.ParentID ?? 0)?.Code ?? 0,
            MinLines = row.MinLines ?? 0,
            MaxLines = row.MaxLines ?? 0,
            TemplateType = row.TemplateType ?? string.Empty,
            PrintTarget = row.PrintTarget ?? default,
            DocOrientation = row.RotateOn180 ?? 0,
            ScriptData = row.ScriptData ?? string.Empty,
            FastReport = row.FastReportDocument ?? [],
            MasterSettings = row.MasterSettings ?? string.Empty
        }).ToList();
    }

    private async Task<Dictionary<int, List<DocumentKindTemplate>>?> ReadLinksByKind()
    {
        if (_ctx.DocKindTemplates == null || _ctx.DocumentTemplates == null)
            return null;

        var links = await _ctx.DocKindTemplates.AsNoTracking().ToListAsync();
        var templates = await _ctx.DocumentTemplates.AsNoTracking()
            .ToDictionaryAsync(template => template.Id, template => template.Code);

        return links
            .Where(link => link.DocKindId is int)
            .GroupBy(link => link.DocKindId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => new DocumentKindTemplate
                {
                    Code = link.Code ?? 0,
                    TemplateCode = link.DocTemplID is int templateId ? templates.GetValueOrDefault(templateId) : 0,
                    Preview = link.Preview == 1,
                    CopyCount = link.CopyCount ?? 0,
                    Operation = link.Operation ?? DocOperationEnum.Close,
                    Act = link.Act ?? DocOpeartionActEnum.CloseDocument,
                    UseCopyCount = link.UseCopyCount == 1,
                    PrintEmpty = link.PrintEmpty == 1,
                    DividePrintGroups = link.DividePrintGroups == 1,
                    CancelOnErrors = link.CancelOnErrors == 1
                }).ToList());
    }

    private async Task<List<DocumentPrintGroup>?> ReadPrintGroups()
    {
        if (_ctx.PrintGroups == null)
            return null;

        var rows = await _ctx.PrintGroups.AsNoTracking().Where(row => row.Deleted == 0).ToListAsync();
        return rows.Select(row => new DocumentPrintGroup
        {
            Code = row.Code,
            Name = row.Name,
            Text = row.Text ?? string.Empty,
            ProposeForCheque = row.ProposeForCheque == 1,
            ProposeForCopy = row.ProposeForCopy == 1,
            ProposeForPrecheque = row.ProposeForPrecheque == 1,
            CloseSessionReport = row.CloseSessionReport == 1,
            TaxSystem = row.TaxSystem ?? 0
        }).ToList();
    }

    private async Task<List<DocumentComment>?> ReadComments()
    {
        if (_ctx.Comments == null)
            return null;

        var rows = await _ctx.Comments.AsNoTracking().Where(row => (row.Deleted ?? 0) == 0).ToListAsync();
        return rows.Select(row => new DocumentComment
        {
            Code = row.Code ?? 0,
            Name = row.Name ?? string.Empty,
            Text = row.Text ?? string.Empty
        }).ToList();
    }

    private async Task ApplyComments(IReadOnlyList<DocumentComment> comments)
    {
        var incoming = LastBy(comments, comment => comment.Code);
        var keep = incoming.Select(comment => comment.Code).ToHashSet();
        var existing = await Alive(_ctx.Comments!);
        var byCode = PreferActive(existing, row => row.Code ?? 0, row => row.Deleted ?? 0);

        foreach (var item in incoming)
        {
            if (!byCode.TryGetValue(item.Code, out var row))
            {
                row = new FrontolComment
                {
                    Id = await NewId(),
                    Code = item.Code,
                    Deleted = 0
                };
                _ctx.Comments!.Add(row);
                byCode[item.Code] = row;
            }
            else
            {
                row.Deleted = 0;
            }

            var title = Title(item.Name, item.Text);
            row.Name = title;
            row.Text = title;
        }

        RemoveAbsent(existing.Where(row => (row.Deleted ?? 0) == 0 && row.Code is int code && !keep.Contains(code)), _ctx.Comments!);
    }

    // Deleted не пишем как признак удаления: это метка внутренней синхронизации Frontol.
    // TaxGroupID не меняем, пока нет справочника налогов.
    private async Task ApplyPrintGroups(IReadOnlyList<DocumentPrintGroup> groups)
    {
        var incoming = LastBy(groups, group => group.Code);
        var keep = incoming.Select(group => group.Code).ToHashSet();
        var existing = await Alive(_ctx.PrintGroups!);
        var byCode = PreferActive(existing, row => row.Code, row => row.Deleted);

        foreach (var item in incoming)
        {
            if (!byCode.TryGetValue(item.Code, out var row))
            {
                row = new PrintGroup
                {
                    Id = await NewId(),
                    Code = item.Code,
                    Deleted = 0,
                    TaxGroupId = 0
                };
                _ctx.PrintGroups!.Add(row);
                byCode[item.Code] = row;
            }
            else
            {
                row.Deleted = 0;
            }

            var title = Title(item.Name, item.Text);
            row.Name = title;
            row.Text = title;
            row.ProposeForCheque = Flag(item.ProposeForCheque);
            row.ProposeForCopy = Flag(item.ProposeForCopy);
            row.ProposeForPrecheque = Flag(item.ProposeForPrecheque);
            row.CloseSessionReport = Flag(item.CloseSessionReport);
            row.TaxSystem = item.TaxSystem;
        }

        RemoveAbsent(existing.Where(row => row.Deleted == 0 && !keep.Contains(row.Code)), _ctx.PrintGroups!);
    }

    private async Task ApplyTemplates(IReadOnlyList<DocumentTemplate> templates)
    {
        var incoming = LastBy(templates, template => template.Code);
        var keep = incoming.Select(template => template.Code).ToHashSet();
        var existing = await Alive(_ctx.DocumentTemplates!);
        var byCode = existing.GroupBy(template => template.Code).ToDictionary(group => group.Key, group => group.First());

        foreach (var item in incoming.Where(template => template.IsFolder))
            await UpsertTemplate(item, byCode);

        foreach (var item in incoming.Where(template => !template.IsFolder))
            await UpsertTemplate(item, byCode);

        var removing = existing.Where(template => !keep.Contains(template.Code)).ToList();
        DetachFromRemovedParents(byCode.Values, removing);
        await RemoveLinks(link => removing.Any(template => template.Id == link.DocTemplID));
        RemoveAbsent(removing.Where(template => (template.ParentID ?? 0) != 0), _ctx.DocumentTemplates!);
        RemoveAbsent(removing.Where(template => (template.ParentID ?? 0) == 0), _ctx.DocumentTemplates!);
    }

    // Группа шаблонов лежит в корне. Элемент ссылается на группу по коду; 0 — без группы.
    private async Task UpsertTemplate(
        DocumentTemplate item,
        Dictionary<int, DocumentPrintFormTemplate> byCode)
    {
        var parentId = ParentId(item, byCode);
        if (parentId == null)
        {
            _logger.LogWarning(
                "Шаблон {Code} пропущен: группа {Parent} не найдена или группа вложена в группу",
                item.Code,
                item.ParentCode);
            return;
        }

        if (!byCode.TryGetValue(item.Code, out var row))
        {
            row = new DocumentPrintFormTemplate
            {
                Id = await NewId(),
                Code = item.Code
            };
            _ctx.DocumentTemplates!.Add(row);
            byCode[item.Code] = row;
        }

        row.Name = item.Name ?? string.Empty;
        row.IsFolder = Flag(item.IsFolder);
        row.ParentID = parentId.Value;
        row.MinLines = item.MinLines;
        row.MaxLines = item.MaxLines;
        row.TemplateType = item.TemplateType ?? string.Empty;
        row.PrintTarget = item.PrintTarget;
        row.RotateOn180 = item.DocOrientation;
        row.ScriptData = item.ScriptData ?? string.Empty;
        row.FastReportDocument = item.FastReport ?? [];
        row.MasterSettings = item.MasterSettings ?? string.Empty;
    }

    private static int? ParentId(DocumentTemplate item, IReadOnlyDictionary<int, DocumentPrintFormTemplate> byCode)
    {
        if (item.IsFolder)
            return item.ParentCode == 0 ? 0 : null;

        if (item.ParentCode == 0)
            return 0;

        if (!byCode.TryGetValue(item.ParentCode, out var parent) || parent.IsFolder != 1)
            return null;

        return parent.Id;
    }

    private void DetachFromRemovedParents(
        IEnumerable<DocumentPrintFormTemplate> rows,
        List<DocumentPrintFormTemplate> removing)
    {
        var removingIds = removing.Select(template => template.Id).ToHashSet();
        foreach (var template in rows)
        {
            var parentId = template.ParentID ?? 0;
            if (removingIds.Contains(template.Id) || parentId == 0 || !removingIds.Contains(parentId))
                continue;

            _logger.LogWarning("Шаблон {Code}: группа удалена, привязка к группе сброшена", template.Code);
            template.ParentID = 0;
        }
    }

    private async Task ApplyKinds(IReadOnlyList<DocumentKind> kinds)
    {
        var incoming = LastBy(kinds, kind => kind.Code);
        var keep = incoming.Select(kind => kind.Code).ToHashSet();
        var existing = await Alive(_ctx.DocKinds!);
        var byCode = existing.GroupBy(kind => kind.Code).ToDictionary(group => group.Key, group => group.First());

        foreach (var item in incoming)
        {
            if (!byCode.TryGetValue(item.Code, out var row))
            {
                row = new DocKind
                {
                    Id = await NewId(),
                    Code = item.Code
                };
                _ctx.DocKinds!.Add(row);
                byCode[item.Code] = row;
            }

            var title = Title(item.Name, item.Text);
            row.Name = title;
            row.Text = title;
            row.DocumentTypeIdentificator = item.Identif ?? string.Empty;
            row.ReceiptType = item.ReceiptType;
            row.AskComment = item.AskComment;
            row.AskEmployee = item.AskEmployee;
            row.CopyEmployee = item.CopyEmployee;
            row.AskCommentOnOpen = item.AskCommentOnOpen;
            row.Recompense = item.Recompense;
        }

        foreach (var item in incoming)
        {
            if (item.DocumentKindTemplates == null)
                continue;

            await ApplyKindTemplates(byCode[item.Code], item.DocumentKindTemplates);
        }

        var removing = existing.Where(kind => !keep.Contains(kind.Code)).ToList();
        await RemoveLinks(link => removing.Any(kind => kind.Id == link.DocKindId));
        RemoveAbsent(removing, _ctx.DocKinds!);
    }

    private async Task ApplyKindTemplates(DocKind kind, IReadOnlyList<DocumentKindTemplate> links)
    {
        var incoming = LastBy(links, link => link.Code);
        var templates = (await Alive(_ctx.DocumentTemplates!))
            .GroupBy(template => template.Code)
            .ToDictionary(group => group.Key, group => group.First());
        var existing = (await Alive(_ctx.DocKindTemplates!))
            .Where(link => link.DocKindId == kind.Id)
            .ToList();
        var keep = new HashSet<int>();

        foreach (var item in incoming)
        {
            var current = existing.FirstOrDefault(link => link.Code == item.Code);
            if (!templates.TryGetValue(item.TemplateCode, out var template))
            {
                _logger.LogWarning(
                    "Строка вида документа {Code} пропущена: нет шаблона {Template}",
                    item.Code,
                    item.TemplateCode);
                if (current != null)
                    keep.Add(current.Id);
                continue;
            }

            if (!ActAllowed(item.Operation, item.Act))
            {
                _logger.LogWarning(
                    "Строка вида {Kind} код {Code} пропущена: операция {Operation} не допускает действие {Act}",
                    kind.Code,
                    item.Code,
                    (int)item.Operation,
                    (int)item.Act);
                if (current != null)
                    keep.Add(current.Id);
                continue;
            }

            if (current == null)
            {
                current = new DocKindTemplate
                {
                    Id = await NewId(),
                    Code = item.Code,
                    DocKindId = kind.Id
                };
                _ctx.DocKindTemplates!.Add(current);
                existing.Add(current);
            }

            current.DocTemplID = template.Id;
            current.Preview = Flag(item.Preview);
            current.CopyCount = item.CopyCount;
            current.Operation = item.Operation;
            current.Act = item.Act;
            current.UseCopyCount = Flag(item.UseCopyCount);
            current.PrintEmpty = Flag(item.PrintEmpty);
            current.DividePrintGroups = Flag(item.DividePrintGroups);
            current.CancelOnErrors = Flag(item.CancelOnErrors);
            keep.Add(current.Id);
        }

        RemoveAbsent(existing.Where(link => !keep.Contains(link.Id)), _ctx.DocKindTemplates!);
    }

    private async Task RemoveLinks(Func<DocKindTemplate, bool> match)
    {
        if (_ctx.DocKindTemplates == null)
            return;

        var removing = (await Alive(_ctx.DocKindTemplates)).Where(match).ToList();
        RemoveAbsent(removing, _ctx.DocKindTemplates);
    }

    private static void RemoveAbsent<T>(IEnumerable<T> removing, DbSet<T> set) where T : class
    {
        var rows = removing.ToList();
        if (rows.Count == 0)
            return;

        set.RemoveRange(rows);
    }

    // Запрос не возвращает строки, добавленные в этом же Apply и ещё не сохранённые.
    private async Task<List<T>> Alive<T>(DbSet<T> set) where T : class
    {
        await set.ToListAsync();
        return set.Local.Where(row => _ctx.Entry(row).State != EntityState.Deleted).ToList();
    }

    private Task<int> NewId() => _mainDb.NextChangeId();

    private string? MissingTable(DocumentConfiguration configuration)
    {
        if (configuration.Comments != null && _ctx.Comments == null)
            return "Не удалось открыть COMMENT";

        if (configuration.PrintGroups != null && _ctx.PrintGroups == null)
            return "Не удалось открыть PRINTGROUP";

        if (configuration.DocumentTemplates != null && _ctx.DocumentTemplates == null)
            return "Не удалось открыть DOCTEMPLATE";

        if (configuration.DocumentKinds != null && _ctx.DocKinds == null)
            return "Не удалось открыть DOCKIND";

        if (configuration.DocumentKinds == null
            || configuration.DocumentKinds.All(kind => kind.DocumentKindTemplates == null))
            return null;

        if (_ctx.DocKindTemplates == null)
            return "Не удалось открыть DOCKINDTMPL";

        if (_ctx.DocumentTemplates == null)
            return "Не удалось открыть DOCTEMPLATE";

        return null;
    }

    private static bool IsUntouched(DocumentConfiguration configuration) =>
        configuration.DocumentKinds == null
        && configuration.DocumentTemplates == null
        && configuration.PrintGroups == null
        && configuration.Comments == null;

    private static List<T> LastBy<T, TKey>(IReadOnlyList<T> rows, Func<T, TKey> key)
        where TKey : notnull
        => rows.GroupBy(key).Select(group => group.Last()).ToList();

    private static Dictionary<TKey, T> PreferActive<T, TKey>(
        IEnumerable<T> rows,
        Func<T, TKey> key,
        Func<T, int> deleted)
        where TKey : notnull
        => rows.GroupBy(key).ToDictionary(group => group.Key, group => group.OrderBy(deleted).First());

    private const int TitleLimit = 100;
    private const int IdentifLimit = 3;

    // В NAME и TEXT пишется одна строка, а TEXT короче NAME (100 против 128).
    // Длиннее лимита Firebird отклонит SaveChanges и оборвёт весь пакет настроек.
    private static string? TooLong(DocumentConfiguration configuration)
    {
        if (configuration.Comments != null)
        {
            foreach (var item in configuration.Comments)
            {
                if (Title(item.Name, item.Text).Length > TitleLimit)
                    return $"Комментарий {item.Code}: имя длиннее {TitleLimit} символов";
            }
        }

        if (configuration.PrintGroups != null)
        {
            foreach (var item in configuration.PrintGroups)
            {
                if (Title(item.Name, item.Text).Length > TitleLimit)
                    return $"Группа печати {item.Code}: имя длиннее {TitleLimit} символов";
            }
        }

        if (configuration.DocumentTemplates != null)
        {
            foreach (var item in configuration.DocumentTemplates)
            {
                if ((item.Name ?? string.Empty).Length > TitleLimit)
                    return $"Шаблон {item.Code}: имя длиннее {TitleLimit} символов";
            }
        }

        if (configuration.DocumentKinds == null)
            return null;

        foreach (var item in configuration.DocumentKinds)
        {
            if (Title(item.Name, item.Text).Length > TitleLimit)
                return $"Вид документа {item.Code}: имя длиннее {TitleLimit} символов";

            if ((item.Identif ?? string.Empty).Length > IdentifLimit)
                return $"Вид документа {item.Code}: identif длиннее {IdentifLimit} символов";
        }

        return null;
    }

    private static string Title(string? name, string? text) =>
        !string.IsNullOrEmpty(name) ? name : text ?? string.Empty;

    private static int Flag(bool value) => value ? 1 : 0;
}
