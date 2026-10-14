using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace AtolLicenseSrvClient.Tests;

[TestFixture]
public class AtolLicenseProtocolTests
{
    [Test]
    public void Encrypt_совпадает_с_блоком_LicenseManager()
    {
        var cipher = AtolLicenseCipher.Encrypt("getLicenseList"u8.ToArray());

        Assert.That(Convert.ToBase64String(cipher), Is.EqualTo("nATfub9rO6pBNttDNOEp2A=="));
    }

    [Test]
    public void Decrypt_снимает_дополнение_нулями()
    {
        var plain = "лицензия"u8.ToArray();
        var decrypted = Encoding.UTF8.GetString(AtolLicenseCipher.Decrypt(AtolLicenseCipher.Encrypt(plain))).TrimEnd('\0');

        Assert.That(decrypted, Is.EqualTo("лицензия"));
    }

    [Test]
    public void Request_собирает_кадр_и_возвращает_тот_же_запрос()
    {
        var frame = AtolLicenseFrame.Request("getLicenseList", 1_700_000_000);
        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(frame);

        Assert.That(frame.Length, Is.EqualTo(payloadLength + 4));

        var nameLength = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(4));
        Assert.That(Encoding.ASCII.GetString(frame, 8, nameLength), Is.EqualTo(AtolLicenseFrame.AppName));
        Assert.That(frame[8 + nameLength], Is.EqualTo(1));

        var plain = AtolLicenseFrame.PlainText(frame.AsSpan(4));
        Assert.That(plain, Does.Contain("\"method\": \"getLicenseList\""));
        Assert.That(plain, Does.Contain("\"timestamp\": 1700000000"));
    }

    [Test]
    public void PlainText_не_принимает_байт_длины_7B_за_начало_json()
    {
        var inner = "{\"method\": \"getLicenseList\"}";
        var data = Convert.ToBase64String(AtolLicenseCipher.Encrypt(Encoding.UTF8.GetBytes(inner)));
        var outer = "{\"data\":\"" + data + "\",\"version\":1}";
        while (Encoding.UTF8.GetByteCount(outer) % 256 != 123)
            outer = outer.Insert(outer.Length - 1, " ");

        var plain = AtolLicenseFrame.PlainText(Frame(outer).AsSpan(4));

        Assert.That(plain, Does.Contain("\"method\": \"getLicenseList\""));
    }

    [Test]
    public void PlainText_битая_длина_имени_это_ошибка_кадра()
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(payload, int.MaxValue);

        Assert.That(
            () => AtolLicenseFrame.PlainText(payload),
            Throws.InstanceOf<InvalidDataException>());
    }

    [Test]
    public void Parse_раскладывает_срок_статус_и_ставит_истекающие_раньше()
    {
        var now = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        var soon = now.AddDays(10).ToUnixTimeSeconds();
        var later = now.AddDays(40).ToUnixTimeSeconds();
        var expired = now.AddDays(-2).ToUnixTimeSeconds();
        var json = $$"""
            {
              "list": [
                {
                  "productName": "Позже",
                  "licenseId": "LATER",
                  "expirationDate": {{later}},
                  "activationDate": 1700000000,
                  "status": 1,
                  "connectionCount": 1,
                  "tariffName": "",
                  "isTrial": 0
                },
                {
                  "productName": "Бессрочная",
                  "licenseId": "FOREVER",
                  "expirationDate": 7258204799,
                  "status": 2,
                  "validMessage": "не активирована",
                  "connectionCount": 4,
                  "tariffName": "Базовый",
                  "isTrial": 1
                },
                {
                  "productId": "FRONTOL",
                  "licenseId": "SOON",
                  "expirationDate": {{soon}},
                  "status": 1,
                  "connectionCount": 2
                },
                {
                  "productName": "Истекла",
                  "licenseId": "OLD",
                  "expirationDate": {{expired}},
                  "status": 3,
                  "connectionCount": 0
                }
              ]
            }
            """;

        var licenses = AtolLicenseListParser.Parse(json, now).Licenses;

        Assert.That(licenses.Select(license => license.Id), Is.EqualTo(new[] { "OLD", "SOON", "LATER", "FOREVER" }));

        var soonLicense = licenses[1];
        Assert.That(soonLicense.ProductName, Is.EqualTo("FRONTOL"));
        Assert.That(soonLicense.TariffDaysLeft, Is.EqualTo(10));
        Assert.That(soonLicense.StatusText, Is.EqualTo("OK"));
        Assert.That(soonLicense.ConnectionCount, Is.EqualTo(2));
        Assert.That(soonLicense.ExpiresAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(soon).UtcDateTime));

        var forever = licenses[3];
        Assert.That(forever.IsPerpetual, Is.True);
        Assert.That(forever.TariffDaysLeft, Is.EqualTo(-1));
        Assert.That(forever.IsTariff, Is.True);
        Assert.That(forever.TariffName, Is.EqualTo("Базовый"));
        Assert.That(forever.IsTrial, Is.True);
        Assert.That(forever.StatusText, Is.EqualTo("не активирована"));
        Assert.That(forever.ActivatedAt, Is.EqualTo(default(DateTime)));

        Assert.That(licenses[0].TariffDaysLeft, Is.EqualTo(-2));
        Assert.That(licenses[0].StatusText, Is.EqualTo("статус 3"));
        Assert.That(licenses[2].UpdatedAt, Is.EqualTo(now.UtcDateTime));
    }

    [Test]
    public void Parse_час_после_истечения_это_минус_день()
    {
        var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var expired = now.AddHours(-1).ToUnixTimeSeconds();
        var json = $$"""
            { "list": [ { "licenseId": "OLD", "expirationDate": {{expired}} } ] }
            """;

        var license = AtolLicenseListParser.Parse(json, now).Licenses.Single();

        Assert.That(license.TariffDaysLeft, Is.EqualTo(-1));
        Assert.That(license.ExpiresAt, Is.Not.Null);
    }

    [Test]
    public void Parse_без_даты_окончания_не_считает_дни()
    {
        var now = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        var json = """{ "list": [ { "licenseId": "NODATE", "productName": "Касса" } ] }""";

        var license = AtolLicenseListParser.Parse(json, now).Licenses.Single();

        Assert.That(license.TariffDaysLeft, Is.EqualTo(0));
        Assert.That(license.ExpiresAt, Is.Null);
        Assert.That(license.IsPerpetual, Is.False);
    }

    [Test]
    public void Parse_isTrial_true_это_пробная()
    {
        var now = DateTimeOffset.UnixEpoch;
        var json = """{ "list": [ { "licenseId": "T", "isTrial": true, "expirationDate": 1893456000 } ] }""";

        var license = AtolLicenseListParser.Parse(json, now).Licenses.Single();

        Assert.That(license.IsTrial, Is.True);
    }

    [Test]
    public void Parse_пустой_ответ_не_даёт_лицензий()
    {
        var now = DateTimeOffset.UnixEpoch;

        Assert.That(AtolLicenseListParser.Parse("", now).Licenses, Is.Empty);
        Assert.That(AtolLicenseListParser.Parse("   ", now).Licenses, Is.Empty);
        Assert.That(AtolLicenseListParser.Parse("{\"list\":[]}", now).HasList, Is.True);
        Assert.That(AtolLicenseListParser.Parse("{\"list\":[]}", now).Licenses, Is.Empty);
        Assert.That(AtolLicenseListParser.Parse("{\"error\":\"нет\"}", now).HasList, Is.False);
        Assert.That(AtolLicenseListParser.Parse("{\"error\":\"нет\"}", now).Licenses, Is.Empty);
    }

    [Test]
    public void Parse_битый_json_бросает_исключение()
    {
        Assert.That(
            () => AtolLicenseListParser.Parse("{", DateTimeOffset.UnixEpoch),
            Throws.InstanceOf<JsonException>());
    }

    private static byte[] Frame(string outerJson)
    {
        var outerBytes = Encoding.UTF8.GetBytes(outerJson);
        var name = Encoding.ASCII.GetBytes(AtolLicenseFrame.AppName);
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
}
