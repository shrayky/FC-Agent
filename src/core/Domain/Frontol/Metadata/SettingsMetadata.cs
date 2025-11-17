using System.Reflection;
using Domain.Attributes;

namespace Domain.Frontol.Metadata;

public record SettingProperty(PropertyInfo Property, string Name);

public static class SettingsMetadata<TConfig>
{
    public static readonly IReadOnlyList<SettingProperty> Properties;

    static SettingsMetadata()
    {
        Properties = typeof(TConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => new SettingProperty(p, GetSettingName(p)))
            .ToArray();
    }

    private static string GetSettingName(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<SettingNameAttribute>();
        return attr?.Name ?? prop.Name;
    }
}