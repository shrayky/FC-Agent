using Domain.Frontol.Metadata;
using FrontolDatabase.Entitys;

namespace FrontolDatabase.Mapping;

public static class SettingsMappingExtensions
{
    public static TConfig ApplyFromSettings<TConfig>(
        this IEnumerable<Settings> settings,
        Func<string, Type, object?> valueParser)
        where TConfig : new()
    {
        var result = new TConfig();
        var dict = SettingsRows.ByLowestId(settings);

        foreach (var meta in SettingsMetadata<TConfig>.Properties)
        {
            if (!dict.TryGetValue(meta.Name, out var setting))
                continue;

            var parsed = valueParser(setting.Value, meta.Property.PropertyType);

            if (parsed is not null)
                meta.Property.SetValue(result, parsed);
        }

        return result;
    }
    
    public static void ApplyToSettings<TConfig>(
        this TConfig config,
        IList<Settings> settings,
        Func<object, string> valueFormatter)
    {
        // Повтор NAME пишем во все строки: касса может прочитать любую копию.
        var groups = SettingsRows.Named(settings)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var meta in SettingsMetadata<TConfig>.Properties)
        {
            if (!groups.TryGetValue(meta.Name, out var namedRows))
                continue;

            var val = meta.Property.GetValue(config);
            if (val is null)
                continue;

            var value = valueFormatter(val);

            // атол немного "накосячил" с типом одной настройки:
            // вроде тип тоавр+нет, а значение как для товар+да+нет
            // пробуем костылить
            if (meta.Name == "ControlAlco")
            {
                value = value == "0" ? "0" : "2";
            }

            SettingsRows.SetValue(namedRows, value);
        }
    }
}