namespace Domain.Agent;

public readonly record struct DiskSmartFacts(int? LifePercent, long? BadBlocks, int? PowerOnDays);

public static class DiskSmartParser
{
    // Порядок = приоритет, как в CrystalDiskInfo: берём первый Current 1–100.
    private static readonly byte[] SsdLifeAttributeIds =
    [
        0xE7, // 231 SSD Life Left
        0xA9, // 169 Remaining Lifetime
        0xCA, // 202 Percent Lifetime Remain
        0xE9, // 233 Media Wearout Indicator
        0xB1, // 177 Wear Leveling Count
        0xE8, // 232 Available Reserved Space
    ];

    public static DiskSmartFacts FromNvme(ReadOnlySpan<byte> healthLog)
    {
        int? life = null;
        if (healthLog.Length > 5)
            life = Math.Max(0, 100 - healthLog[5]);

        int? days = null;
        if (healthLog.Length >= 136)
            days = HoursToDays((long)BitConverter.ToUInt64(healthLog.Slice(128, 8)));

        return new DiskSmartFacts(life, null, days);
    }

    public static DiskSmartFacts FromAta(ReadOnlySpan<byte> smart, string kind)
    {
        var attributes = ReadAttributes(smart);
        var days = HoursToDays(Raw(attributes, 0x09));

        if (kind == DiskKind.Hdd)
            return new DiskSmartFacts(null, Raw(attributes, 0x05), days);

        return new DiskSmartFacts(SsdLife(attributes), null, days);
    }

    public static string KindFromAta(ReadOnlySpan<byte> smart) =>
        SsdLife(ReadAttributes(smart)) is not null ? DiskKind.Ssd : DiskKind.Hdd;

    // ATA IDENTIFY: модель в словах 27–46, байты в слове переставлены.
    public static string ModelFromAtaIdentify(ReadOnlySpan<byte> identify)
    {
        const int offset = 54;
        const int length = 40;
        if (identify.Length < offset + length)
            return string.Empty;

        var chars = new char[length];
        for (var i = 0; i < length; i += 2)
        {
            chars[i] = (char)identify[offset + i + 1];
            chars[i + 1] = (char)identify[offset + i];
        }

        return new string(chars).Trim('\0', ' ');
    }

    private static int? HoursToDays(long? hours)
    {
        if (hours is null)
            return null;

        return (int)Math.Min(hours.Value / 24, int.MaxValue);
    }

    // Current wear-атрибута — оставшаяся жизнь SSD.
    private static int? SsdLife(IReadOnlyDictionary<byte, AtaAttribute> attributes)
    {
        foreach (var id in SsdLifeAttributeIds)
        {
            if (!attributes.TryGetValue(id, out var attribute))
                continue;

            if (attribute.Current is > 0 and <= 100)
                return attribute.Current;
        }

        return null;
    }

    private static long? Raw(IReadOnlyDictionary<byte, AtaAttribute> attributes, byte id)
    {
        if (!attributes.TryGetValue(id, out var attribute))
            return null;

        return attribute.Raw;
    }

    private static Dictionary<byte, AtaAttribute> ReadAttributes(ReadOnlySpan<byte> smart)
    {
        var result = new Dictionary<byte, AtaAttribute>();
        if (smart.Length < 2)
            return result;

        for (var offset = 2; offset + 12 <= smart.Length && offset < 2 + 30 * 12; offset += 12)
        {
            var id = smart[offset];
            if (id == 0)
                continue;

            long raw = 0;
            for (var i = 5; i >= 0; i--)
                raw = (raw << 8) | smart[offset + 5 + i];

            result[id] = new AtaAttribute(smart[offset + 3], raw);
        }

        return result;
    }

    private readonly record struct AtaAttribute(byte Current, long Raw);
}
