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
        var dict = settings.ToDictionary(s => s.Name, s => s.Value);

        foreach (var meta in SettingsMetadata<TConfig>.Properties)
        {
            if (!dict.TryGetValue(meta.Name, out var raw))
                continue;

            var parsed = valueParser(raw, meta.Property.PropertyType);

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
        var dict = settings.ToDictionary(s => s.Name, s => s);

        foreach (var meta in SettingsMetadata<TConfig>.Properties)
        {
            if (!dict.TryGetValue(meta.Name, out var setting))
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

            setting.Value = value;
        }
    }
}