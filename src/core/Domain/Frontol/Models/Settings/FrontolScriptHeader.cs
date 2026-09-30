using System.Text.RegularExpressions;

namespace Domain.Frontol.Models.Settings;

public static class FrontolScriptHeader
{
    private static readonly Regex DeclarationRegex = new(
        $@"^\s*(?:var|let|const)\s+{FrontolScriptLibraryDirectory.ConstantName}\s*=",
        RegexOptions.Compiled);

    private static readonly Regex LiteralRegex = new(
        $@"^(?<lead>\s*(?:var|let|const)\s+{FrontolScriptLibraryDirectory.ConstantName}\s*=\s*)(?<quote>[""'])(?<body>(?:[^""\\]|\\.)*)\k<quote>(?<tail>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex ValueRegex = new(
        $@"^(?<lead>\s*(?:var|let|const)\s+{FrontolScriptLibraryDirectory.ConstantName}\s*=\s*)(?<value>[^;]*?)\s*(?<tail>;.*)?$",
        RegexOptions.Compiled);

    public static int LibraryPathLineIndex(string? scriptText)
    {
        if (string.IsNullOrWhiteSpace(scriptText))
            return -1;

        var lines = SplitLines(scriptText);

        for (var index = 0; index < lines.Count; index++)
        {
            if (DeclarationRegex.IsMatch(lines[index].Content))
                return index;
        }

        return -1;
    }

    public static string WithLibraryPath(string? scriptText, string libraryPath)
    {
        // Пустой скрипт — объявлять нечего: иначе агент создаст ActionScript с одной константой.
        if (string.IsNullOrWhiteSpace(scriptText))
            return scriptText ?? string.Empty;

        // Каталог не задан — писать нечего, чужой текст не трогаем.
        if (string.IsNullOrWhiteSpace(libraryPath))
            return scriptText;

        var escaped = libraryPath.Replace("\\", "\\\\").Replace("\"", "\\\"");

        var lineIndex = LibraryPathLineIndex(scriptText);

        if (lineIndex < 0)
            return Header(libraryPath, escaped) + scriptText;

        var lines = SplitLines(scriptText);
        var (content, ending) = lines[lineIndex];
        var replaced = ReplaceValue(content, escaped);

        if (replaced == content)
            return scriptText;

        lines[lineIndex] = (replaced, ending);

        return string.Concat(lines.Select(line => line.Content + line.Ending));
    }

    // Сравнивается экранированный литерал: без этого уже правильный скрипт переписывался бы при каждой выгрузке.
    // Хвост вроде `+ "\\libs"` — не комментарий: заменяется вся правая часть, иначе останется склейка со старым куском.
    private static string ReplaceValue(string line, string escaped)
    {
        var literal = LiteralRegex.Match(line);

        if (literal.Success && IsSemicolonAndComment(literal.Groups["tail"].Value))
        {
            if (literal.Groups["body"].Value == escaped)
                return line;

            return literal.Groups["lead"].Value + "\"" + escaped + "\"" + literal.Groups["tail"].Value;
        }

        var value = ValueRegex.Match(line);

        if (!value.Success)
            return line;

        return value.Groups["lead"].Value + "\"" + escaped + "\"" + (value.Groups["tail"].Success ? value.Groups["tail"].Value : ";");
    }

    private static bool IsSemicolonAndComment(string tail)
    {
        var rest = tail.TrimStart();
        if (rest.Length == 0)
            return true;

        if (rest[0] == ';')
            rest = rest[1..].TrimStart();

        if (rest.Length == 0)
            return true;

        if (rest.StartsWith("//", StringComparison.Ordinal))
            return true;

        if (!rest.StartsWith("/*", StringComparison.Ordinal))
            return false;

        var end = rest.IndexOf("*/", 2, StringComparison.Ordinal);
        return end >= 0 && rest[(end + 2)..].Trim().Length == 0;
    }

    private static string Header(string path, string escaped) =>
        $"// Библиотеки скриптов fc-agent: loadLibraryScript(\"имя-файла\") подключает {path}\\имя-файла.js.\r\n"
        + $"var {FrontolScriptLibraryDirectory.ConstantName} = \"{escaped}\";\r\n\r\n";

    private static List<(string Content, string Ending)> SplitLines(string text)
    {
        var lines = new List<(string Content, string Ending)>();
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
                continue;

            var end = index > start && text[index - 1] == '\r' ? index - 1 : index;

            lines.Add((text[start..end], text[end..(index + 1)]));
            start = index + 1;
        }

        if (start < text.Length)
            lines.Add((text[start..], string.Empty));

        return lines;
    }
}
