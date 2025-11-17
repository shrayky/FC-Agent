namespace Domain.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingNameAttribute : Attribute
{
    public string Name { get; }

    public SettingNameAttribute(string name)
    {
        Name = name;
    }
}