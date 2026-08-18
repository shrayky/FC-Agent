namespace Domain.Frontol.Models.Settings;

public record FrontolParameter
{
    // идентификатор параметра Frontol (имя в таблице SETTINGS)
    public string Id { get; set; } = string.Empty;

    // значение параметра (всегда строка)
    public string Value { get; set; } = string.Empty;
}
