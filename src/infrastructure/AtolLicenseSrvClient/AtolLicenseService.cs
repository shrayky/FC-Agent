using System.Buffers.Binary;
using System.Net.Sockets;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Microsoft.Extensions.Logging;

namespace AtolLicenseSrvClient;

public class AtolLicenseService : IAtolLicenseSource
{
    private const string Host = "127.0.0.1";
    private const int Port = 756;
    private const int MaxReplyBytes = 1_048_576;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<AtolLicenseService> _logger;

    public AtolLicenseService(ILogger<AtolLicenseService> logger)
    {
        _logger = logger;
    }

    // Пустой список, обрыв и битый кадр не затирают лицензии в состоянии агента:
    // вместо ответа менеджера уходят имена файлов из каталога AtolLicSvc.
    public async Task<AtolLicenseRead> Read(CancellationToken cancellationToken = default)
    {
        try
        {
            var parsed = await Query(cancellationToken);
            if (parsed.HasList && parsed.Licenses.Count > 0)
                return new AtolLicenseRead(parsed.Licenses, false);

            if (parsed.HasList)
                _logger.LogDebug("Менеджер лицензий АТОЛ вернул пустой список, читаем файлы");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Менеджер лицензий АТОЛ недоступен, читаем файлы");
        }

        return new AtolLicenseRead(FromFiles(), true);
    }

    private async Task<AtolLicenseListParse> Query(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        var now = DateTimeOffset.UtcNow;
        using var client = new TcpClient();
        await client.ConnectAsync(Host, Port, timeout.Token);

        await using var stream = client.GetStream();
        var frame = AtolLicenseFrame.Request("getLicenseList", now.ToUnixTimeSeconds());
        await stream.WriteAsync(frame, timeout.Token);

        var lengthBytes = await ReadExact(stream, 4, timeout.Token);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
        if (length is <= 0 or > MaxReplyBytes)
            throw new InvalidDataException($"Менеджер лицензий АТОЛ вернул кадр длиной {length}");

        var payload = await ReadExact(stream, length, timeout.Token);
        var plain = AtolLicenseFrame.PlainText(payload);
        if (string.IsNullOrWhiteSpace(plain))
            return new(true, []);

        var parsed = AtolLicenseListParser.Parse(plain, now);
        if (!parsed.HasList)
            _logger.LogWarning("Менеджер лицензий АТОЛ вернул ответ без списка: {Body}", Brief(plain));

        return parsed;
    }

    private static string Brief(string text)
    {
        const int limit = 200;
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= limit ? flat : flat[..limit];
    }

    private static async Task<byte[]> ReadExact(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var got = 0;
        while (got < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(got, count - got), cancellationToken);
            if (read == 0)
                throw new IOException("Менеджер лицензий АТОЛ закрыл соединение");
            got += read;
        }

        return buffer;
    }

    private List<LicenseInformation> FromFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Файлы лицензий АТОЛ читаются только в Windows");
            return [];
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var licenseFolderPath = Path.Combine(programData, "Atol", "AtolLicSvc", "license");
        if (!Directory.Exists(licenseFolderPath))
        {
            _logger.LogDebug("Нет каталога лицензий {Folder}", licenseFolderPath);
            return [];
        }

        var files = Directory.GetFiles(licenseFolderPath);
        _logger.LogDebug("В каталоге {Folder} найдено {Count} файлов лицензий", licenseFolderPath, files.Length);

        var licenses = new List<LicenseInformation>(files.Length);
        foreach (var licFile in files)
        {
            licenses.Add(new LicenseInformation
            {
                Id = Path.GetFileNameWithoutExtension(licFile)
            });
        }

        return licenses;
    }
}
