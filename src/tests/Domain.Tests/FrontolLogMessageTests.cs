using Domain.Frontol;

namespace Domain.Tests;

[TestFixture]
public class FrontolLogMessageTests
{
    /// <summary>
    /// К тексту ошибки добавляется предыдущая запись журнала.
    /// </summary>
    [Test]
    public void WithPrevious_дописывает_предыдущее_сообщение()
    {
        var result = FrontolLogMessage.WithPrevious(
            "Ошибка отправки данных в Alco Unit",
            "получены данные из Alco Unit: ErrCode 400");

        Assert.That(result, Is.EqualTo(
            $"Ошибка отправки данных в Alco Unit{Environment.NewLine}получены данные из Alco Unit: ErrCode 400"));
    }

    /// <summary>
    /// Последовательности \uXXXX в предыдущей записи становятся читаемым текстом.
    /// </summary>
    [Test]
    public void WithPrevious_раскодирует_unicode_escape()
    {
        var result = FrontolLogMessage.WithPrevious(
            "HTTP/1.1 400 Bad Request",
            @"получены данные из Alco Unit: \u041D\u0435 \u0443\u0434\u0430\u043B\u043E\u0441\u044C");

        Assert.That(result, Is.EqualTo(
            $"HTTP/1.1 400 Bad Request{Environment.NewLine}получены данные из Alco Unit: Не удалось"));
    }

    /// <summary>
    /// Пустая предыдущая запись не меняет текст ошибки.
    /// </summary>
    [Test]
    public void WithPrevious_без_предыдущего_возвращает_ошибку()
    {
        Assert.That(FrontolLogMessage.WithPrevious("ошибка", null), Is.EqualTo("ошибка"));
        Assert.That(FrontolLogMessage.WithPrevious("ошибка", "  "), Is.EqualTo("ошибка"));
    }
}
