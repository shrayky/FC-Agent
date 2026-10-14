using System.Text.Json;
using Domain.Frontol.Models;

namespace AtolLicenseSrvClient;

public static class AtolLicenseListParser
{
    // 2199-12-31 в ответе менеджера — бессрочная лицензия, не календарная дата.
    private const long PerpetualUnixSeconds = 7_000_000_000;
    private const long MaxUnixSeconds = 253_402_300_799;

    public static AtolLicenseListParse Parse(string plainJson, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(plainJson))
            return new(false, []);

        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(plainJson));
        using var document = JsonDocument.ParseValue(ref reader);
        if (!document.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            return new(false, []);

        var licenses = new List<LicenseInformation>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
                licenses.Add(Map(item, now));
        }

        licenses.Sort((left, right) => SortKey(left).CompareTo(SortKey(right)));
        return new(true, licenses);
    }

    private static LicenseInformation Map(JsonElement item, DateTimeOffset now)
    {
        var expiration = Int64(item, "expirationDate");
        var perpetual = expiration >= PerpetualUnixSeconds;
        var knownExpiration = expiration > 0;
        var product = Text(item, "productName");
        if (product.Length == 0)
            product = Text(item, "productId");

        var tariff = Text(item, "tariffName");
        var message = Text(item, "validMessage");
        var status = (int)Int64(item, "status");

        return new LicenseInformation
        {
            Id = Text(item, "licenseId"),
            ProductName = product,
            IsTariff = tariff.Length > 0,
            TariffName = tariff,
            ActivatedAt = Unix(Int64(item, "activationDate")),
            ExpiresAt = knownExpiration ? Unix(expiration) : null,
            IsPerpetual = perpetual,
            // Деление long к нулю даёт 0 в первые сутки после истечения.
            TariffDaysLeft = perpetual ? -1 : knownExpiration ? DaysLeft(expiration, now) : 0,
            StatusText = Status(status, message),
            ConnectionCount = (int)Int64(item, "connectionCount"),
            IsTrial = Flag(item, "isTrial"),
            UpdatedAt = now.UtcDateTime
        };
    }

    private static int DaysLeft(long expiration, DateTimeOffset now) =>
        (int)Math.Floor((expiration - now.ToUnixTimeSeconds()) / 86400d);

    private static long SortKey(LicenseInformation license) =>
        license.IsPerpetual ? long.MaxValue : license.TariffDaysLeft;

    private static string Status(int status, string message)
    {
        if (status == 1)
            return "OK";
        return message.Length > 0 ? message : $"статус {status}";
    }

    private static DateTime Unix(long seconds)
    {
        if (seconds <= 0 || seconds > MaxUnixSeconds)
            return default;
        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }

    private static string Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
    }

    private static bool Flag(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => Int64(value) != 0,
            _ => false
        };
    }

    private static long Int64(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return 0;
        return Int64(value);
    }

    private static long Int64(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
            return 0;
        if (value.TryGetInt64(out var number))
            return number;
        return value.TryGetDouble(out var floating) ? (long)floating : 0;
    }
}
