using Domain.Sales;

namespace Domain.Tests;

[TestFixture]
public class SalesCursorStateTests
{
    [Test]
    public void Set_запоминает_номер_и_помечает_инициализацию()
    {
        var cursor = new SalesCursorState();

        Assert.That(cursor.Initialized, Is.False);
        Assert.That(cursor.LastDocumentNumber, Is.EqualTo(0));

        cursor.Set(1540);

        Assert.That(cursor.Initialized, Is.True);
        Assert.That(cursor.LastDocumentNumber, Is.EqualTo(1540));
    }

    [Test]
    public void Set_принимает_ноль_как_валидный_курсор()
    {
        var cursor = new SalesCursorState();

        cursor.Set(0);

        Assert.That(cursor.Initialized, Is.True);
        Assert.That(cursor.LastDocumentNumber, Is.EqualTo(0));
    }
}
