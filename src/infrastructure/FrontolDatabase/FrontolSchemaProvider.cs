namespace FrontolDatabase;

/// <summary>
/// Отдаёт схему подключённой базы. Чтение отложено до первого обращения, поэтому оно идёт
/// по той же строке подключения, которой пользуется EF, и не зависит от того, была ли база
/// доступна в момент старта приложения (иначе схема молча терялась и модель строилась как раньше).
/// </summary>
public class FrontolSchemaProvider
{
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, FrontolSchema> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly FrontolSchema? _fixedSchema;
    private readonly Lock _lock = new();

    /// <summary>Провайдер по умолчанию: одно чтение на процесс и строку подключения.</summary>
    public static FrontolSchemaProvider Shared { get; } = new();

    public FrontolSchemaProvider()
    {
    }

    /// <summary>Провайдер с заранее заданной схемой: используется в тестах.</summary>
    internal FrontolSchemaProvider(FrontolSchema schema) => _fixedSchema = schema;

    public FrontolSchema Get(string? connectionString)
    {
        if (_fixedSchema is not null)
            return _fixedSchema;

        lock (_lock)
        {
            var key = connectionString ?? string.Empty;

            if (_cache.TryGetValue(key, out var cached) && !IsRetryDue(cached))
                return cached;

            var schema = Read(connectionString);
            _cache[key] = schema;

            return schema;
        }
    }

    private static FrontolSchema Read(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return FrontolSchema.Failed("строка подключения к базе Frontol не задана");

        try
        {
            return FrontolSchema.Read(connectionString);
        }
        catch (Exception ex)
        {
            return FrontolSchema.Failed(ex.Message);
        }
    }

    /// <summary>Провалившееся чтение повторяем, чтобы приложение могло подняться раньше базы.</summary>
    private static bool IsRetryDue(FrontolSchema schema)
        => !schema.IsKnown && DateTime.UtcNow - schema.ResolvedAt > RetryAfterFailure;
}
