using Domain.Frontol.Enums;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class DocumentConfigurationRepositoryTests
{
    private MainDbCtx _db = null!;
    private DocumentConfigurationRepository _repository = null!;
    private int _nextId = 9000;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MainDbCtx(options);
        _db.DocKinds = _db.Set<DocKind>();
        _db.DocKindTemplates = _db.Set<DocKindTemplate>();
        _db.DocumentTemplates = _db.Set<DocumentPrintFormTemplate>();
        _db.PrintGroups = _db.Set<PrintGroup>();
        _db.Comments = _db.Set<FrontolComment>();
        _db.SaveChanges();

        _nextId = 9000;
        var mainDb = new Mock<IFrontolMainDb>();
        mainDb.Setup(m => m.NextChangeId()).Returns(() => Task.FromResult(_nextId++));

        _repository = new DocumentConfigurationRepository(
            new Mock<ILogger<DocumentConfigurationRepository>>().Object,
            _db,
            mainDb.Object);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void Модели_пакета_не_содержат_Id()
    {
        Assert.That(typeof(DocumentKind).GetProperty("Id"), Is.Null);
        Assert.That(typeof(DocumentTemplate).GetProperty("Id"), Is.Null);
        Assert.That(typeof(DocumentKindTemplate).GetProperty("Id"), Is.Null);
        Assert.That(typeof(DocumentPrintGroup).GetProperty("Id"), Is.Null);
        Assert.That(typeof(DocumentComment).GetProperty("Id"), Is.Null);
    }

    [Test]
    public void Допустимы_только_пары_операции_и_действия_из_описания()
    {
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Close, DocOpeartionActEnum.PrintMark), Is.True);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Close, DocOpeartionActEnum.PrintBill), Is.False);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Copy, DocOpeartionActEnum.PrintCopy), Is.True);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Copy, DocOpeartionActEnum.CloseDocument), Is.False);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Mark, DocOpeartionActEnum.PrintMark), Is.True);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Postpone, DocOpeartionActEnum.PrintCopy), Is.True);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Cancel, DocOpeartionActEnum.PrintMark), Is.True);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Bill, DocOpeartionActEnum.PrintBill), Is.True);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Bill, DocOpeartionActEnum.PrintCopy), Is.False);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Reserve, DocOpeartionActEnum.PrintCopy), Is.True);
        Assert.That(DocumentConfigurationRepository.ActAllowed(DocOperationEnum.Reserve, DocOpeartionActEnum.PrintMark), Is.False);
    }

    [Test]
    public async Task Read_отдаёт_коды_связей_и_не_включает_удалённые_строки()
    {
        _db.DocKinds!.Add(new DocKind { Id = 10, Code = 2, Name = "Продажа", Text = "Продажа", DocumentTypeIdentificator = "ПР" });
        _db.DocumentTemplates!.Add(new DocumentPrintFormTemplate { Id = 20, Code = 1, Name = "Группа", IsFolder = 1 });
        _db.DocumentTemplates.Add(new DocumentPrintFormTemplate
        {
            Id = 21,
            Code = 5,
            Name = "Чек",
            ParentID = 20,
            PrintTarget = PrintTargetEnum.EcrTape,
            ScriptData = "print",
            FastReportDocument = [1, 2],
            MasterSettings = "wizard"
        });
        _db.DocKindTemplates!.Add(new DocKindTemplate
        {
            Id = 30,
            Code = 1,
            DocKindId = 10,
            DocTemplID = 21,
            Operation = DocOperationEnum.Close,
            Act = DocOpeartionActEnum.PrintCopy
        });
        _db.PrintGroups!.Add(new PrintGroup { Id = 40, Code = 3, Name = "Кухня", Text = "Кухня", Deleted = 0, TaxSystem = 2, ProposeForCheque = 1 });
        _db.PrintGroups.Add(new PrintGroup { Id = 41, Code = 4, Name = "Скрытая", Deleted = 1 });
        _db.Comments!.Add(new FrontolComment { Id = 50, Code = 6, Name = "Отказ", Text = "Отказ" });
        _db.Comments.Add(new FrontolComment { Id = 51, Code = 7, Name = "Старый", Text = "Старый", Deleted = 1 });
        await _db.SaveChangesAsync();

        var result = await _repository.Read();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.DocumentKinds!.Select(kind => kind.Code), Is.EqualTo(new[] { 2 }));
        Assert.That(result.Value.DocumentTemplates!.Single(template => template.Code == 5).ParentCode, Is.EqualTo(1));
        Assert.That(result.Value.DocumentTemplates!.Single(template => template.Code == 5).FastReport, Is.EqualTo(new byte[] { 1, 2 }));
        Assert.That(result.Value.DocumentKinds!.Single().DocumentKindTemplates!.Single().TemplateCode, Is.EqualTo(5));
        Assert.That(result.Value.PrintGroups!.Select(group => group.Code), Is.EqualTo(new[] { 3 }));
        Assert.That(result.Value.PrintGroups!.Single().ProposeForCheque, Is.True);
        Assert.That(result.Value.Comments!.Select(comment => comment.Code), Is.EqualTo(new[] { 6 }));
    }

    [Test]
    public async Task Apply_новая_запись_пишет_код_связи_и_не_выдаёт_Id_снаружи()
    {
        var result = await _repository.Apply(Packet());

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");

        var comment = _db.Comments!.Single();
        Assert.That(comment.Id, Is.EqualTo(9000));
        Assert.That(comment.Code, Is.EqualTo(6));
        Assert.That(comment.Name, Is.EqualTo("Отказ"));
        Assert.That(comment.Text, Is.EqualTo("Отказ"));

        var group = _db.PrintGroups!.Single();
        Assert.That(group.Id, Is.EqualTo(9001));
        Assert.That(group.Name, Is.EqualTo("Кухня"));
        Assert.That(group.Text, Is.EqualTo("Кухня"));
        Assert.That(group.TaxGroupId, Is.EqualTo(0));
        Assert.That(group.TaxSystem, Is.EqualTo(2));
        Assert.That(group.ProposeForCheque, Is.EqualTo(1));

        var folder = _db.DocumentTemplates!.Single(template => template.Code == 1);
        var template = _db.DocumentTemplates!.Single(template => template.Code == 5);
        Assert.That(template.ParentID, Is.EqualTo(folder.Id));
        Assert.That(template.ScriptData, Is.EqualTo("print"));
        Assert.That(template.FastReportDocument, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(template.MasterSettings, Is.EqualTo("wizard"));
        Assert.That(template.PrintTarget, Is.EqualTo(PrintTargetEnum.EcrTape));

        var kind = _db.DocKinds!.Single();
        Assert.That(kind.Name, Is.EqualTo("Продажа"));
        Assert.That(kind.Text, Is.EqualTo("Продажа"));
        Assert.That(kind.DocumentTypeIdentificator, Is.EqualTo("ПР"));
        Assert.That(kind.ReceiptType, Is.EqualTo(ReceiptTypeEnum.Sell));
        Assert.That(kind.AskComment, Is.True);

        var link = _db.DocKindTemplates!.Single();
        Assert.That(link.DocKindId, Is.EqualTo(kind.Id));
        Assert.That(link.DocTemplID, Is.EqualTo(template.Id));
        Assert.That(link.CopyCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Apply_существующий_код_сохраняет_Id_и_налоговую_группу()
    {
        _db.PrintGroups!.Add(new PrintGroup
        {
            Id = 3,
            Code = 3,
            Name = "старая",
            Deleted = 0,
            TaxGroupId = 15
        });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(new DocumentConfiguration
        {
            PrintGroups =
            [
                new DocumentPrintGroup { Code = 3, Name = "Кухня", TaxSystem = 1, ProposeForCopy = true }
            ]
        });

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        var group = _db.PrintGroups!.Single();
        Assert.That(group.Id, Is.EqualTo(3));
        Assert.That(group.Name, Is.EqualTo("Кухня"));
        Assert.That(group.Text, Is.EqualTo("Кухня"));
        Assert.That(group.TaxGroupId, Is.EqualTo(15));
        Assert.That(group.ProposeForCopy, Is.EqualTo(1));
    }

    [Test]
    public async Task Apply_удаляет_строки_которых_нет_в_пакете()
    {
        _db.DocKinds!.Add(new DocKind { Id = 1, Code = 1, Name = "Оставить" });
        _db.DocKinds.Add(new DocKind { Id = 2, Code = 2, Name = "Убрать" });
        _db.DocumentTemplates!.Add(new DocumentPrintFormTemplate { Id = 20, Code = 1, Name = "Группа", IsFolder = 1 });
        _db.DocKindTemplates!.Add(new DocKindTemplate { Id = 30, Code = 1, DocKindId = 2, DocTemplID = 20 });
        _db.PrintGroups!.Add(new PrintGroup { Id = 40, Code = 3, Name = "Живая", Deleted = 0 });
        _db.PrintGroups.Add(new PrintGroup { Id = 41, Code = 4, Name = "Метка", Deleted = 1 });
        _db.PrintGroups.Add(new PrintGroup { Id = 42, Code = 9, Name = "Лишняя", Deleted = 0 });
        _db.Comments!.Add(new FrontolComment { Id = 50, Code = 6, Name = "Отказ", Text = "Отказ" });
        _db.Comments.Add(new FrontolComment { Id = 51, Code = 8, Name = "Лишний", Text = "Лишний" });
        _db.Comments.Add(new FrontolComment { Id = 52, Code = 7, Name = "Метка", Text = "Метка", Deleted = 1 });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(new DocumentConfiguration
        {
            DocumentKinds = [new DocumentKind { Code = 1, Name = "Оставить" }],
            DocumentTemplates = [new DocumentTemplate { Code = 1, Name = "Группа", IsFolder = true }],
            PrintGroups = [new DocumentPrintGroup { Code = 3, Name = "Живая" }],
            Comments = [new DocumentComment { Code = 6, Name = "Отказ" }]
        });

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(_db.DocKinds!.Select(kind => kind.Code), Is.EqualTo(new[] { 1 }));
        Assert.That(_db.DocKindTemplates!.Count(), Is.EqualTo(0));
        Assert.That(_db.PrintGroups!.Select(group => group.Code).OrderBy(code => code), Is.EqualTo(new[] { 3, 4 }));
        Assert.That(_db.PrintGroups!.Single(group => group.Code == 4).Deleted, Is.EqualTo(1));
        Assert.That(_db.Comments!.Select(comment => comment.Code).OrderBy(code => code), Is.EqualTo(new[] { 6, 7 }));
    }

    [Test]
    public async Task Apply_null_секции_не_трогает_таблицу()
    {
        _db.Comments!.Add(new FrontolComment { Id = 50, Code = 6, Name = "Отказ", Text = "Отказ" });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(new DocumentConfiguration());

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_db.Comments!.Single().Name, Is.EqualTo("Отказ"));
    }

    [Test]
    public async Task Apply_null_строк_вида_не_трогает_DOCKINDTMPL_а_пустой_список_удаляет()
    {
        _db.DocKinds!.Add(new DocKind { Id = 10, Code = 2, Name = "Продажа" });
        _db.DocumentTemplates!.Add(new DocumentPrintFormTemplate { Id = 21, Code = 5, Name = "Чек" });
        _db.DocKindTemplates!.Add(new DocKindTemplate
        {
            Id = 30,
            Code = 1,
            DocKindId = 10,
            DocTemplID = 21,
            CopyCount = 2
        });
        await _db.SaveChangesAsync();

        var untouched = await _repository.Apply(new DocumentConfiguration
        {
            DocumentKinds = [new DocumentKind { Code = 2, Name = "Продажа", DocumentKindTemplates = null }]
        });

        Assert.That(untouched.IsSuccess, Is.True, untouched.IsFailure ? untouched.Error : "");
        Assert.That(_db.DocKindTemplates!.Single().CopyCount, Is.EqualTo(2));

        var cleared = await _repository.Apply(new DocumentConfiguration
        {
            DocumentKinds = [new DocumentKind { Code = 2, Name = "Продажа", DocumentKindTemplates = [] }]
        });

        Assert.That(cleared.IsSuccess, Is.True, cleared.IsFailure ? cleared.Error : "");
        Assert.That(_db.DocKindTemplates!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Apply_строка_длиннее_колонки_не_меняет_таблицу()
    {
        _db.PrintGroups!.Add(new PrintGroup { Id = 40, Code = 3, Name = "Кухня", Text = "Кухня", Deleted = 0 });
        _db.DocKinds!.Add(new DocKind { Id = 10, Code = 2, Name = "Продажа", DocumentTypeIdentificator = "ПР" });
        _db.DocumentTemplates!.Add(new DocumentPrintFormTemplate { Id = 21, Code = 5, Name = "Чек" });
        _db.Comments!.Add(new FrontolComment { Id = 50, Code = 6, Name = "Отказ", Text = "Отказ" });
        await _db.SaveChangesAsync();

        var group = await _repository.Apply(new DocumentConfiguration
        {
            PrintGroups = [new DocumentPrintGroup { Code = 3, Name = new string('Я', 101) }]
        });
        Assert.That(group.IsFailure, Is.True);
        Assert.That(group.Error, Is.EqualTo("Группа печати 3: имя длиннее 100 символов"));
        Assert.That(_db.PrintGroups!.Single().Name, Is.EqualTo("Кухня"));

        var kindName = await _repository.Apply(new DocumentConfiguration
        {
            DocumentKinds = [new DocumentKind { Code = 2, Name = new string('Я', 101) }]
        });
        Assert.That(kindName.Error, Is.EqualTo("Вид документа 2: имя длиннее 100 символов"));
        Assert.That(_db.DocKinds!.Single().Name, Is.EqualTo("Продажа"));

        var identif = await _repository.Apply(new DocumentConfiguration
        {
            DocumentKinds = [new DocumentKind { Code = 2, Name = "Продажа", Identif = "АБВГ" }]
        });
        Assert.That(identif.Error, Is.EqualTo("Вид документа 2: identif длиннее 3 символов"));
        Assert.That(_db.DocKinds!.Single().DocumentTypeIdentificator, Is.EqualTo("ПР"));

        var template = await _repository.Apply(new DocumentConfiguration
        {
            DocumentTemplates = [new DocumentTemplate { Code = 5, Name = new string('Я', 101) }]
        });
        Assert.That(template.Error, Is.EqualTo("Шаблон 5: имя длиннее 100 символов"));
        Assert.That(_db.DocumentTemplates!.Single().Name, Is.EqualTo("Чек"));

        var comment = await _repository.Apply(new DocumentConfiguration
        {
            Comments = [new DocumentComment { Code = 6, Text = new string('Я', 101) }]
        });
        Assert.That(comment.Error, Is.EqualTo("Комментарий 6: имя длиннее 100 символов"));
        Assert.That(_db.Comments!.Single().Name, Is.EqualTo("Отказ"));

        var exact = await _repository.Apply(new DocumentConfiguration
        {
            PrintGroups = [new DocumentPrintGroup { Code = 3, Name = new string('Я', 100) }]
        });
        Assert.That(exact.IsSuccess, Is.True, exact.IsFailure ? exact.Error : "");
        Assert.That(_db.PrintGroups!.Single().Name, Has.Length.EqualTo(100));
    }

    [Test]
    public async Task Apply_недопустимая_пара_не_перезаписывает_строку()
    {
        _db.DocKinds!.Add(new DocKind { Id = 10, Code = 2, Name = "Продажа" });
        _db.DocumentTemplates!.Add(new DocumentPrintFormTemplate { Id = 21, Code = 5, Name = "Чек" });
        _db.DocKindTemplates!.Add(new DocKindTemplate
        {
            Id = 30,
            Code = 1,
            DocKindId = 10,
            DocTemplID = 21,
            Operation = DocOperationEnum.Close,
            Act = DocOpeartionActEnum.PrintCopy,
            CopyCount = 1
        });
        await _db.SaveChangesAsync();

        var result = await _repository.Apply(new DocumentConfiguration
        {
            DocumentKinds =
            [
                new DocumentKind
                {
                    Code = 2,
                    Name = "Продажа",
                    DocumentKindTemplates =
                    [
                        new DocumentKindTemplate
                        {
                            Code = 1,
                            TemplateCode = 5,
                            Operation = DocOperationEnum.Close,
                            Act = DocOpeartionActEnum.PrintBill,
                            CopyCount = 9
                        }
                    ]
                }
            ]
        });

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        var link = _db.DocKindTemplates!.Single();
        Assert.That(link.Act, Is.EqualTo(DocOpeartionActEnum.PrintCopy));
        Assert.That(link.CopyCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Apply_группа_шаблонов_во_вложении_пропускается()
    {
        var result = await _repository.Apply(new DocumentConfiguration
        {
            DocumentTemplates =
            [
                new DocumentTemplate { Code = 2, Name = "Вложенная", IsFolder = true, ParentCode = 1 }
            ]
        });

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(_db.DocumentTemplates!.Count(), Is.EqualTo(0));
    }

    private static DocumentConfiguration Packet() => new()
    {
        Comments = [new DocumentComment { Code = 6, Name = "Отказ" }],
        PrintGroups = [new DocumentPrintGroup { Code = 3, Name = "Кухня", TaxSystem = 2, ProposeForCheque = true }],
        DocumentTemplates =
        [
            new DocumentTemplate { Code = 1, Name = "Группа", IsFolder = true, TemplateType = "sale" },
            new DocumentTemplate
            {
                Code = 5,
                Name = "Чек",
                ParentCode = 1,
                PrintTarget = PrintTargetEnum.EcrTape,
                ScriptData = "print",
                FastReport = [1, 2, 3],
                MasterSettings = "wizard"
            }
        ],
        DocumentKinds =
        [
            new DocumentKind
            {
                Code = 2,
                Name = "Продажа",
                Text = "другое",
                Identif = "ПР",
                ReceiptType = ReceiptTypeEnum.Sell,
                AskComment = true,
                DocumentKindTemplates =
                [
                    new DocumentKindTemplate
                    {
                        Code = 1,
                        TemplateCode = 5,
                        Operation = DocOperationEnum.Close,
                        Act = DocOpeartionActEnum.PrintCopy,
                        CopyCount = 2
                    }
                ]
            }
        ]
    };
}
