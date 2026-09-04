using Domain.Agent;

namespace Domain.Tests;

[TestFixture]
public class DiskSmartParserTests
{
    [Test]
    public void FromNvme_жизнь_это_100_минус_percentage_used()
    {
        var log = new byte[512];
        log[5] = 12;

        var facts = DiskSmartParser.FromNvme(log);

        Assert.That(facts.LifePercent, Is.EqualTo(88));
        Assert.That(facts.BadBlocks, Is.Null);
    }

    [Test]
    public void FromNvme_percentage_used_больше_100_даёт_0()
    {
        var log = new byte[512];
        log[5] = 255;

        var facts = DiskSmartParser.FromNvme(log);

        Assert.That(facts.LifePercent, Is.EqualTo(0));
    }

    [Test]
    public void FromNvme_наработка_из_power_on_hours()
    {
        var log = new byte[512];
        BitConverter.TryWriteBytes(log.AsSpan(128), 48UL);

        var facts = DiskSmartParser.FromNvme(log);

        Assert.That(facts.PowerOnDays, Is.EqualTo(2));
    }

    [Test]
    public void FromNvme_короткий_буфер_без_данных()
    {
        var facts = DiskSmartParser.FromNvme([1, 2, 3]);

        Assert.That(facts.LifePercent, Is.Null);
        Assert.That(facts.PowerOnDays, Is.Null);
    }

    [Test]
    public void FromAta_hdd_бэды_из_атрибута_05()
    {
        var smart = AtaSmart((Id: 0x05, Current: 100, Raw: 7), (Id: 0x09, Current: 100, Raw: 72));

        var facts = DiskSmartParser.FromAta(smart, DiskKind.Hdd);

        Assert.That(facts.BadBlocks, Is.EqualTo(7));
        Assert.That(facts.LifePercent, Is.Null);
        Assert.That(facts.PowerOnDays, Is.EqualTo(3));
    }

    [Test]
    public void FromAta_ssd_жизнь_из_атрибута_231()
    {
        var smart = AtaSmart((Id: 0xE7, Current: 94, Raw: 0), (Id: 0x09, Current: 100, Raw: 24));

        var facts = DiskSmartParser.FromAta(smart, DiskKind.Ssd);

        Assert.That(facts.LifePercent, Is.EqualTo(94));
        Assert.That(facts.BadBlocks, Is.Null);
        Assert.That(facts.PowerOnDays, Is.EqualTo(1));
    }

    [Test]
    public void FromAta_ssd_жизнь_из_первого_известного_атрибута()
    {
        var smart = AtaSmart((Id: 0xA9, Current: 80, Raw: 0), (Id: 0xE7, Current: 94, Raw: 0));

        var facts = DiskSmartParser.FromAta(smart, DiskKind.Ssd);

        Assert.That(facts.LifePercent, Is.EqualTo(94));
    }

    [Test]
    public void FromAta_нет_атрибутов_всё_null()
    {
        var facts = DiskSmartParser.FromAta(new byte[512], DiskKind.Hdd);

        Assert.That(facts.LifePercent, Is.Null);
        Assert.That(facts.BadBlocks, Is.Null);
        Assert.That(facts.PowerOnDays, Is.Null);
    }

    [Test]
    public void KindFromAta_life_атрибут_это_ssd()
    {
        var smart = AtaSmart((Id: 0xE7, Current: 73, Raw: 0), (Id: 0x09, Current: 100, Raw: 45984));

        Assert.That(DiskSmartParser.KindFromAta(smart), Is.EqualTo(DiskKind.Ssd));
    }

    [Test]
    public void KindFromAta_без_life_это_hdd()
    {
        var smart = AtaSmart((Id: 0x05, Current: 100, Raw: 7), (Id: 0x09, Current: 100, Raw: 72));

        Assert.That(DiskSmartParser.KindFromAta(smart), Is.EqualTo(DiskKind.Hdd));
    }

    [Test]
    public void ModelFromAtaIdentify_байты_в_слове_переставлены()
    {
        var identify = new byte[512];
        WriteAtaIdentifyModel(identify, "SAMSUNG SSD 860");

        Assert.That(DiskSmartParser.ModelFromAtaIdentify(identify), Is.EqualTo("SAMSUNG SSD 860"));
    }

    [Test]
    public void ModelFromAtaIdentify_короткий_буфер_пустая_строка()
    {
        Assert.That(DiskSmartParser.ModelFromAtaIdentify([1, 2, 3]), Is.Empty);
    }

    private static byte[] AtaSmart(params (byte Id, byte Current, long Raw)[] attributes)
    {
        var buffer = new byte[512];
        var offset = 2;
        foreach (var attribute in attributes)
        {
            buffer[offset] = attribute.Id;
            buffer[offset + 3] = attribute.Current;
            var raw = BitConverter.GetBytes(attribute.Raw);
            Array.Copy(raw, 0, buffer, offset + 5, 6);
            offset += 12;
        }

        return buffer;
    }

    private static void WriteAtaIdentifyModel(byte[] identify, string model)
    {
        var padded = model.PadRight(40);
        for (var i = 0; i < 40; i += 2)
        {
            identify[54 + i] = (byte)padded[i + 1];
            identify[54 + i + 1] = (byte)padded[i];
        }
    }
}
