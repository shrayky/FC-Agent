using FrontolDatabase.Entitys;

namespace FrontolDatabase.Mapping;

internal static class SettingsRows
{
    // Пустой NAME нельзя класть в словарь: ключ null роняет разбор всей таблицы.
    public static IEnumerable<IGrouping<string, Settings>> Named(IEnumerable<Settings> settings)
        => settings
            .Where(row => !string.IsNullOrEmpty(row.Name))
            .GroupBy(row => row.Name);

    // Повтор NAME: чтение берёт строку с минимальным Id.
    public static Dictionary<string, Settings> ByLowestId(IEnumerable<Settings> settings)
        => Named(settings)
            .ToDictionary(group => group.Key, group => group.MinBy(row => row.Id)!);

    // Frontol может прочитать любую копию NAME, поэтому значение одно на все строки.
    public static void SetValue(IEnumerable<Settings> rows, string value)
    {
        foreach (var row in rows)
            row.Value = value;
    }
}
