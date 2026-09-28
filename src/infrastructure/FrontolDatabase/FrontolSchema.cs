using System.Security.Cryptography;
using System.Text;
using FirebirdSql.Data.FirebirdClient;

namespace FrontolDatabase;

/// <summary>
/// Фактическая схема подключённой базы Frontol: какие таблицы и колонки в ней реально есть.
/// Нужна, чтобы модель EF строилась под конкретную (в том числе старую) версию базы,
/// а не падала на "SQL error code = -206 Column unknown".
/// </summary>
public sealed class FrontolSchema
{
    private readonly Dictionary<string, HashSet<string>> _columns;

    private FrontolSchema(
        Dictionary<string, HashSet<string>> columns,
        string fingerprint,
        bool isKnown,
        string? readError,
        DateTime resolvedAt)
    {
        _columns = columns;
        Fingerprint = fingerprint;
        IsKnown = isKnown;
        ReadError = readError;
        ResolvedAt = resolvedAt;
        ColumnCount = columns.Sum(pair => pair.Value.Count);
    }

    /// <summary>
    /// Схема не читалась: считаем, что все таблицы и колонки на месте — поведение
    /// как до появления проверки схемы. Используется в тестах и как значение по умолчанию.
    /// </summary>
    public static FrontolSchema Unknown { get; } =
        new(new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
            "unknown", false, null, DateTime.MinValue);

    /// <summary>
    /// Отпечаток набора таблиц и колонок. Входит в ключ кеша модели EF:
    /// без него для второй базы (другой версии) переиспользовалась бы уже построенная модель.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>Схема прочитана из базы. Если false — работаем по модели «как раньше».</summary>
    public bool IsKnown { get; }

    /// <summary>Почему схему не удалось прочитать (для лога).</summary>
    public string? ReadError { get; }

    /// <summary>Когда схема была прочитана (или когда попытка чтения провалилась).</summary>
    public DateTime ResolvedAt { get; }

    public int TableCount => _columns.Count;

    public int ColumnCount { get; }

    public bool HasTable(string table)
        => !IsKnown || _columns.ContainsKey(table);

    public bool HasColumn(string table, string column)
        => !IsKnown || (_columns.TryGetValue(table, out var columns) && columns.Contains(column));

    /// <summary>Читает список таблиц и колонок из системного каталога Firebird одним запросом.</summary>
    public static FrontolSchema Read(string connectionString)
    {
        using var connection = new FbConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            select trim(rf.RDB$RELATION_NAME) as tab, trim(rf.RDB$FIELD_NAME) as col
            from RDB$RELATION_FIELDS rf
            join RDB$RELATIONS r on r.RDB$RELATION_NAME = rf.RDB$RELATION_NAME
            where coalesce(rf.RDB$SYSTEM_FLAG, 0) = 0
              and r.RDB$VIEW_BLR is null
            """;

        using var reader = command.ExecuteReader();

        var columns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
            Add(columns, reader.GetString(0), reader.GetString(1));

        if (columns.Count == 0)
            throw new InvalidOperationException("системный каталог Firebird вернул пустой список таблиц");

        return FromColumns(columns);
    }

    /// <summary>Попытка чтения схемы провалилась.</summary>
    internal static FrontolSchema Failed(string error)
        => new(new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
            "unknown", false, error, DateTime.UtcNow);

    /// <summary>Схема, собранная вручную: используется в тестах.</summary>
    internal static FrontolSchema ForTests(params (string Table, string Column)[] columns)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (table, column) in columns)
            Add(map, table, column);

        return FromColumns(map);
    }

    private static FrontolSchema FromColumns(Dictionary<string, HashSet<string>> columns)
        => new(columns, BuildFingerprint(columns), true, null, DateTime.UtcNow);

    private static void Add(Dictionary<string, HashSet<string>> columns, string table, string column)
    {
        if (!columns.TryGetValue(table, out var tableColumns))
            columns[table] = tableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        tableColumns.Add(column);
    }

    private static string BuildFingerprint(Dictionary<string, HashSet<string>> columns)
    {
        var text = string.Join('|', columns
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{string.Join(',', pair.Value.OrderBy(column => column, StringComparer.Ordinal))}"));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        return Convert.ToHexString(hash, 0, 8);
    }
}
