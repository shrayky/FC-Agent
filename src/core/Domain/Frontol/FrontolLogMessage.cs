using System.Globalization;
using System.Text.RegularExpressions;

namespace Domain.Frontol;

/// <summary>
/// Собирает текст ошибки Frontol вместе с предыдущей записью журнала.
/// </summary>
public static class FrontolLogMessage
{
    private static readonly Regex UnicodeEscape = new(@"\\u([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    public static string WithPrevious(string errorMessage, string? previousAction)
    {
        if (string.IsNullOrWhiteSpace(previousAction))
            return errorMessage;

        return $"{errorMessage}{Environment.NewLine}{DecodeUnicodeEscapes(previousAction)}";
    }

    private static string DecodeUnicodeEscapes(string text)
    {
        return UnicodeEscape.Replace(text, match =>
        {
            var code = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((char)code).ToString();
        });
    }
}
