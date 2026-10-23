using System.Text.Json;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Shared.Json;

namespace FrontolDatabase.Tests;

[TestFixture]
public class DocumentPrintFormTemplateTests
{
    /// <summary>
    /// FRDATA — бинарный BLOB FastReport, не текст.
    /// </summary>
    [Test]
    public void FastReportDocument_имеет_тип_byte_array()
    {
        var property = typeof(DocumentPrintFormTemplate)
            .GetProperty(nameof(DocumentPrintFormTemplate.FastReportDocument));

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.PropertyType, Is.EqualTo(typeof(byte[])));
    }

    /// <summary>
    /// В JSON для фронта содержимое уходит как Base64.
    /// </summary>
    [Test]
    public void FastReportDocument_в_json_сериализуется_как_base64()
    {
        var entity = new DocumentPrintFormTemplate
        {
            FastReportDocument = [0x00, 0x01, 0xFF]
        };

        var json = JsonSerializer.Serialize(entity, JsonSerializeOptionsProvider.Default());

        Assert.That(json, Does.Contain("\"FastReportDocument\": \"AAH/\""));
    }

    // Без явного типа "BLOB" строка без длины мапится как ACTSCRIPT.DATA.
    [Test]
    public void ScriptData_и_MasterSettings_мапятся_как_текст_скрипта_действий()
    {
        var builder = new DbContextOptionsBuilder<MainDbCtx>();
        MainDbCtx.UseFrontolFirebird(builder, "database=localhost:dummy.fdb;user=sysdba;password=masterkey");

        using var ctx = new MainDbCtx(builder.Options, new FrontolSchemaProvider(FrontolSchema.Unknown));
        var template = ctx.Model.FindEntityType(typeof(DocumentPrintFormTemplate));
        var actionScript = ctx.Model.FindEntityType(typeof(ActScript));
        var scriptType = actionScript!.FindProperty(nameof(ActScript.Script))!.GetColumnType();

        Assert.That(template!.FindProperty(nameof(DocumentPrintFormTemplate.ScriptData))!.GetColumnType(),
            Is.EqualTo(scriptType));
        Assert.That(template.FindProperty(nameof(DocumentPrintFormTemplate.MasterSettings))!.GetColumnType(),
            Is.EqualTo(scriptType));
        Assert.That(template.FindProperty(nameof(DocumentPrintFormTemplate.FastReportDocument))!.GetColumnType(),
            Is.EqualTo("BLOB"));
    }
}
