using FrontolDatabase.Repositories;

namespace FrontolDatabase.Tests;

[TestFixture]
public class InformAboutChangesInsertTests
{
    [Test]
    public void Вставка_пишет_SYNCCATEG_24()
    {
        var sql = MainDbRepository.InsertInformAboutChangesSql(hasSyncCateg: true);

        Assert.That(sql, Is.EqualTo(
            $"INSERT INTO SETTINGS (ID, NAME, VAL, SYNCCATEG) VALUES ({{0}}, {{1}}, {{2}}, {MainDbRepository.InformAboutChangesSyncCateg})"));
    }

    [Test]
    public void Вставка_без_колонки_не_упоминает_SYNCCATEG()
    {
        var sql = MainDbRepository.InsertInformAboutChangesSql(hasSyncCateg: false);

        Assert.That(sql, Is.EqualTo("INSERT INTO SETTINGS (ID, NAME, VAL) VALUES ({0}, {1}, {2})"));
        Assert.That(sql, Does.Not.Contain("SYNCCATEG"));
    }
}
