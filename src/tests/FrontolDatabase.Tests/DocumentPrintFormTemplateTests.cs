using System.Text.Json;
using FrontolDatabase.Entitys;
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
}
