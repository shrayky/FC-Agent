using System.Text.RegularExpressions;
using Domain.Frontol.Models.Settings;

namespace Domain.Tests;

[TestFixture]
public class FrontolScriptHeaderTests
{
    private const string Path = @"C:\libs";

    // Литерал так, как он записан в файле скрипта: C:\libs с удвоенной обратной косой.
    private const string Literal = @"""C:\\libs""";

    [Test]
    public void WithLibraryPath_дописывает_объявление_в_начало()
    {
        const string text = "function init() {\r\n}\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Does.Contain($"var libPath = {Literal};"));
        Assert.That(result, Does.EndWith(text));
    }

    [Test]
    public void WithLibraryPath_заменяет_другое_значение()
    {
        const string text = "var libPath = \"D:\\\\old\";\r\nvar x = 1;\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo($"var libPath = {Literal};\r\nvar x = 1;\r\n"));
    }

    [Test]
    public void WithLibraryPath_не_трогает_уже_нужное_значение()
    {
        var text = $"var libPath = {Literal};\r\nvar x = 1;\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo(text));
    }

    [Test]
    public void WithLibraryPath_заменяет_пустой_литерал_заготовки()
    {
        const string text = "var libPath = \"\";\r\nfunction init() {\r\n}\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Does.StartWith($"var libPath = {Literal};"));
        Assert.That(result, Does.Not.Contain("\"\""));
    }

    [Test]
    public void WithLibraryPath_экранирует_обратную_косую()
    {
        var result = FrontolScriptHeader.WithLibraryPath("var x = 1;\r\n", @"C:\libs");

        Assert.That(result, Does.Contain(@"var libPath = ""C:\\libs"";"));
    }

    [Test]
    public void WithLibraryPath_сохраняет_отступ_и_меняет_кавычки_на_двойные()
    {
        const string text = "    var libPath = 'старое';\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo($"    var libPath = {Literal};\r\n"));
    }

    [Test]
    public void WithLibraryPath_не_превращает_let_и_const_в_var()
    {
        Assert.That(
            FrontolScriptHeader.WithLibraryPath("let libPath = \"\";\r\n", Path),
            Does.StartWith($"let libPath = {Literal};"));

        Assert.That(
            FrontolScriptHeader.WithLibraryPath("const libPath = \"\";\r\n", Path),
            Does.StartWith($"const libPath = {Literal};"));
    }

    [Test]
    public void WithLibraryPath_сохраняет_хвост_строки_с_комментарием()
    {
        const string text = "var libPath = \"D:\\\\old\"; // каталог библиотек\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo($"var libPath = {Literal}; // каталог библиотек\r\n"));
    }

    [Test]
    public void WithLibraryPath_заменяет_значение_без_литерала()
    {
        const string text = "var libPath = getDefaultPath();\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo($"var libPath = {Literal};\r\n"));
    }

    [Test]
    public void WithLibraryPath_не_считает_объявлением_другой_регистр()
    {
        const string text = "var LibPath = \"D:\\\\old\";\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Does.EndWith(text));
        Assert.That(result, Does.Contain($"var libPath = {Literal};"));
    }

    [Test]
    public void WithLibraryPath_правит_объявление_ниже_заголовка_на_месте()
    {
        var lines = Enumerable.Range(1, 24).Select(number => $"var v{number} = {number};").ToList();
        lines.Add("var libPath = \"D:\\\\old\";");
        lines.Add("var tail = 0;");

        var text = string.Join("\r\n", lines) + "\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(
            result,
            Is.EqualTo(text.Replace("var libPath = \"D:\\\\old\";", $"var libPath = {Literal};")));
        Assert.That(Regex.Matches(result, "var libPath").Count, Is.EqualTo(1));
    }

    [Test]
    public void LibraryPathLineIndex_находит_объявление()
    {
        Assert.That(FrontolScriptHeader.LibraryPathLineIndex("var libPath = \"\";"), Is.EqualTo(0));
        Assert.That(
            FrontolScriptHeader.LibraryPathLineIndex("var a = 1;\r\n    var libPath = \"x\";"),
            Is.EqualTo(1));
    }

    [Test]
    public void LibraryPathLineIndex_минус_один_без_объявления()
    {
        Assert.That(FrontolScriptHeader.LibraryPathLineIndex("var a = 1;\r\nvar LibPath = \"\";"), Is.EqualTo(-1));
        Assert.That(FrontolScriptHeader.LibraryPathLineIndex(string.Empty), Is.EqualTo(-1));
        Assert.That(FrontolScriptHeader.LibraryPathLineIndex("   "), Is.EqualTo(-1));
        Assert.That(FrontolScriptHeader.LibraryPathLineIndex(null), Is.EqualTo(-1));
    }

    [Test]
    public void WithLibraryPath_пустой_скрипт_остаётся_пустым()
    {
        Assert.That(FrontolScriptHeader.WithLibraryPath(null, Path), Is.Empty);
        Assert.That(FrontolScriptHeader.WithLibraryPath(string.Empty, Path), Is.Empty);
        Assert.That(FrontolScriptHeader.WithLibraryPath("   ", Path), Is.EqualTo("   "));
    }

    [Test]
    public void WithLibraryPath_без_каталога_не_трогает_скрипт()
    {
        const string text = "var x = 1;\r\n";

        Assert.That(FrontolScriptHeader.WithLibraryPath(text, string.Empty), Is.EqualTo(text));
        Assert.That(FrontolScriptHeader.WithLibraryPath(text, "   "), Is.EqualTo(text));
    }

    [Test]
    public void WithLibraryPath_заменяет_конкатенацию_целиком()
    {
        const string text = "var libPath = \"D:\\\\old\" + \"\\\\libs\";\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo($"var libPath = {Literal};\r\n"));
    }

    [Test]
    public void WithLibraryPath_конкатенация_сохраняет_комментарий()
    {
        const string text = "var libPath = \"D:\\\\old\" + \"\\\\libs\"; // путь\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo($"var libPath = {Literal}; // путь\r\n"));
    }

    [Test]
    public void WithLibraryPath_не_оставляет_склейку_если_первый_литерал_уже_верный()
    {
        const string text = "var libPath = \"C:\\\\libs\" + \"\\\\x\";\r\n";

        var result = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(result, Is.EqualTo($"var libPath = {Literal};\r\n"));
    }

    [Test]
    public void WithLibraryPath_идемпотентна()
    {
        const string text = "function init() {\r\n}\r\n";

        var once = FrontolScriptHeader.WithLibraryPath(text, Path);

        Assert.That(FrontolScriptHeader.WithLibraryPath(once, Path), Is.EqualTo(once));
    }
}
