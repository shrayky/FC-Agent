using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AtolLicenseSrvClient;

public static class AtolLicenseFrame
{
    // В кадре имя без второй «l»: так его пишет LicenseManager.exe.
    public const string AppName = "AtoLicenseManager";

    // Текст запроса собран как у LicenseManager: сервис сверяет не только поля JSON.
    public static byte[] Request(string method, long unixSeconds)
    {
        var inner =
            "{\n    \"method\": \"" + method + "\",\n    \"parameters\": {\n    },\n    \"timestamp\": " +
            unixSeconds.ToString(CultureInfo.InvariantCulture) + "\n}\n";
        var data = Convert.ToBase64String(AtolLicenseCipher.Encrypt(Encoding.UTF8.GetBytes(inner)));
        var outer = "{\n    \"data\": \"" + data + "\",\n    \"version\": 1\n}\n";
        var outerBytes = Encoding.UTF8.GetBytes(outer);
        var name = Encoding.ASCII.GetBytes(AppName);

        var payloadLength = 4 + name.Length + 1 + 4 + outerBytes.Length;
        var frame = new byte[4 + payloadLength];
        var span = frame.AsSpan();
        BinaryPrimitives.WriteInt32BigEndian(span, payloadLength);
        BinaryPrimitives.WriteInt32BigEndian(span[4..], name.Length);
        name.CopyTo(span[8..]);
        span[8 + name.Length] = 1;
        BinaryPrimitives.WriteInt32BigEndian(span[(9 + name.Length)..], outerBytes.Length);
        outerBytes.CopyTo(span[(13 + name.Length)..]);
        return frame;
    }

    public static string PlainText(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
            throw new InvalidDataException("Короткий кадр менеджера лицензий АТОЛ");

        var nameLength = BinaryPrimitives.ReadInt32BigEndian(payload);
        if (nameLength < 0 || nameLength > payload.Length - 4)
            throw new InvalidDataException("В кадре менеджера лицензий АТОЛ битая длина имени");

        // После имени: байт версии и 4 байта длины JSON. Байт длины 0x7B — это «{»,
        // поиск скобки начинает разбор внутри заголовка.
        var header = payload[(4 + nameLength)..];
        if (header.Length < 5)
            throw new InvalidDataException("Короткий кадр менеджера лицензий АТОЛ");

        var jsonLength = BinaryPrimitives.ReadInt32BigEndian(header[1..]);
        if (jsonLength < 0 || jsonLength > header.Length - 5)
            throw new InvalidDataException("В кадре менеджера лицензий АТОЛ битая длина JSON");

        var reader = new Utf8JsonReader(header.Slice(5, jsonLength));
        using var document = JsonDocument.ParseValue(ref reader);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.String)
            return string.Empty;

        var encoded = data.GetString();
        if (string.IsNullOrEmpty(encoded))
            return string.Empty;

        var plain = AtolLicenseCipher.Decrypt(Convert.FromBase64String(encoded));
        return Encoding.UTF8.GetString(plain).TrimEnd('\0').Trim();
    }
}
