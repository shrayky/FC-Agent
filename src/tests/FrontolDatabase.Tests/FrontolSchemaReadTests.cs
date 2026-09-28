using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FrontolDatabase.Tests;

/// <summary>
/// Чтение схемы с реальной базы Frontol. Тест включается переменной окружения, потому что
/// в CI базы нет, а запрос к системному каталогу Firebird легко ломается «на глазок»
/// (например, RDB$VIEW_BLR лежит в RDB$RELATIONS, а не в RDB$RELATION_FIELDS).
/// </summary>
[TestFixture]
public class FrontolSchemaReadTests
{
    private const string ConnectionStringVariable = "FC_FRONTOL_TEST_DB";

    [Test]
    public void Читает_схему_реальной_базы()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Ignore($"Задайте {ConnectionStringVariable}, например " +
                          @"database=localhost:C:\FBDB\MAIN.GDB;user=SYSDBA;password=masterkey");

        var schema = FrontolSchema.Read(connectionString);

        Assert.That(schema.IsKnown, Is.True);
        Assert.That(schema.TableCount, Is.GreaterThan(0));
        Assert.That(schema.ColumnCount, Is.GreaterThan(0));
        Assert.That(schema.HasTable("PROFILE"), Is.True, "PROFILE есть в любой версии Frontol");
        Assert.That(schema.HasColumn("PROFILE", "ID"), Is.True);
    }

    /// <summary>
    /// Воспроизводит исходную проблему целиком: запрос профилей к базе, где колонки FRONTOLSELFIE нет.
    /// </summary>
    [Test]
    public void Запрос_профилей_работает_на_базе_без_FRONTOLSELFIE()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Ignore($"Задайте {ConnectionStringVariable} — тест выполняет реальный запрос к базе Frontol");

        var schema = FrontolSchema.Read(connectionString);

        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseFirebird(connectionString)
            .ReplaceService<IModelCacheKeyFactory, FrontolModelCacheKeyFactory>()
            .Options;

        // Схему не подкладываем: контекст читает её сам, как в приложении.
        using var ctx = new MainDbCtx(options);
        ctx.UserProfiles = ctx.Set<Profile>();

        Assert.That(ctx.Schema.IsKnown, Is.True, ctx.Schema.ReadError);
        Assert.That(ctx.Schema.Fingerprint, Is.EqualTo(schema.Fingerprint));

        var sql = ctx.UserProfiles!.ToQueryString();

        if (!schema.HasColumn("PROFILE", "FRONTOLSELFIE"))
            Assert.That(sql, Does.Not.Contain("FRONTOLSELFIE"), "колонки нет в базе — её не должно быть и в запросе");

        // Раньше именно здесь падало "SQL error code = -206 Column unknown".
        Assert.DoesNotThrow(() => ctx.UserProfiles!.AsNoTracking().ToList());
    }
}
