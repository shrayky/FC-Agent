using System.Text.RegularExpressions;

namespace FrontolDatabase.Parsers;

/// <summary>
/// Преобразование времени между шаблоном (HH:mm) и SETTINGS.VAL Frontol (Delphi TDateTime).
/// </summary>
public static class FrontolTimeParsers
{
    private static readonly Regex TimeOnly = new(
        @"^(\d{1,2}):(\d{2})(?::(\d{2}))?$",
        RegexOptions.Compiled);

    private static readonly Regex DelphiTime = new(
        @"^\d{1,2}\.\d{1,2}\.\d{4}\s+(\d{1,2}):(\d{1,2})(?::(\d{1,2}))?(?:[:.]\d+)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Готовит значение к записи в SETTINGS.VAL.
    /// </summary>
    public static string ToDb(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        var text = value.Trim();
        if (!TryParse(text, out var hours, out var minutes, out var seconds))
            return text;

        return FormatDelphi(hours, minutes, seconds);
    }

    /// <summary>
    /// Готовит SETTINGS.VAL к передаче в шаблон (HH:mm).
    /// </summary>
    public static string FromDb(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        var text = value.Trim();
        if (!DelphiTime.IsMatch(text))
            return text;

        if (!TryParse(text, out var hours, out var minutes, out _))
            return text;

        return $"{hours:00}:{minutes:00}";
    }

    /// <summary>
    /// Разбирает HH:mm или Delphi-дату Frontol.
    /// </summary>
    private static bool TryParse(string text, out int hours, out int minutes, out int seconds)
    {
        hours = 0;
        minutes = 0;
        seconds = 0;

        var delphi = DelphiTime.Match(text);
        if (delphi.Success)
            return Clamp(delphi.Groups[1].Value, delphi.Groups[2].Value, delphi.Groups[3].Value,
                out hours, out minutes, out seconds);

        var time = TimeOnly.Match(text);
        if (time.Success)
            return Clamp(time.Groups[1].Value, time.Groups[2].Value, time.Groups[3].Value,
                out hours, out minutes, out seconds);

        return false;
    }

    /// <summary>
    /// Ограничивает часы, минуты и секунды допустимым диапазоном.
    /// </summary>
    private static bool Clamp(string hoursRaw, string minutesRaw, string secondsRaw,
        out int hours, out int minutes, out int seconds)
    {
        hours = int.TryParse(hoursRaw, out var parsedHours) ? parsedHours : 0;
        minutes = int.TryParse(minutesRaw, out var parsedMinutes) ? parsedMinutes : 0;
        seconds = int.TryParse(secondsRaw, out var parsedSeconds) ? parsedSeconds : 0;

        if (hours > 23) hours = 23;
        if (minutes > 59) minutes = 59;
        if (seconds > 59) seconds = 59;
        return true;
    }

    /// <summary>
    /// Собирает строку VAL в формате Frontol.
    /// </summary>
    private static string FormatDelphi(int hours, int minutes, int seconds)
    {
        if (hours == 23 && minutes == 59 && seconds == 0)
            seconds = 59;

        return $"30.12.1899 {hours}:{minutes}:{seconds}:0";
    }
}
