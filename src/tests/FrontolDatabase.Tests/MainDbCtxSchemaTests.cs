using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Tests;

/// <summary>
/// Старая база Frontol не содержит части таблиц и колонок: модель должна строиться под фактическую
/// схему, иначе Firebird отвечает "SQL error code = -206 Column unknown".
/// </summary>
[TestFixture]
public class MainDbCtxSchemaTests
{
    private const string ConnectionString = "database=localhost:dummy.fdb;user=sysdba;password=masterkey";

    private static readonly (string Table, string Column)[] ProfileColumns =
    [
        ("PROFILE", "ID"),
        ("PROFILE", "CODE"),
        ("PROFILE", "NAME"),
        ("PROFILE", "SKIPMODE"),
        ("PROFILE", "FRONTOLSELFIE"),
        ("PROFILE", "BASE"),
    ];

    private static MainDbCtx Context(FrontolSchema schema)
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseFirebird(ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, FrontolModelCacheKeyFactory>()
            .Options;

        return new MainDbCtx(options, schema);
    }

    [Test]
    public void Отсутствующая_колонка_исключается_из_модели()
    {
        // PROFILE старой версии: без FRONTOLSELFIE.
        var withoutSelfie = ProfileColumns.Where(column => column.Column != "FRONTOLSELFIE").ToArray();

        using var ctx = Context(FrontolSchema.ForTests(withoutSelfie));
        ctx.UserProfiles = ctx.Set<Profile>();

        var profile = ctx.Model.FindEntityType(typeof(Profile));

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile!.FindProperty(nameof(Profile.ForSelfieUser)), Is.Null,
            "Свойства без колонки в базе быть не должно");
        Assert.That(profile.FindProperty(nameof(Profile.SkipSupervisorMode)), Is.Not.Null);
        Assert.That(profile.FindProperty(nameof(Profile.DontChangeUsersOnExchange)), Is.Not.Null);
    }

    [Test]
    public void Sql_запроса_не_содержит_отсутствующей_колонки()
    {
        var withoutSelfie = ProfileColumns.Where(column => column.Column != "FRONTOLSELFIE").ToArray();

        using var ctx = Context(FrontolSchema.ForTests(withoutSelfie));
        ctx.UserProfiles = ctx.Set<Profile>();

        var sql = ctx.UserProfiles!.ToQueryString();

        Assert.That(sql, Does.Not.Contain("FRONTOLSELFIE"));
        Assert.That(sql, Does.Contain("SKIPMODE"));
    }

    [Test]
    public void Отсутствующая_таблица_исключается_из_модели()
    {
        // База без SETTINGS: остальные таблицы тоже не описаны намеренно.
        using var ctx = Context(FrontolSchema.ForTests(ProfileColumns));

        Assert.That(ctx.Model.FindEntityType(typeof(Settings)), Is.Null);
        Assert.That(ctx.Model.FindEntityType(typeof(Profile)), Is.Not.Null);
    }

    [Test]
    public void Отсутствующая_таблица_оставляет_DbSet_недоступным()
    {
        using var ctx = Context(FrontolSchema.ForTests(ProfileColumns));

        Assert.That(ctx.Settings, Is.Null, $"Settings = {ctx.Settings}");
    }

    [Test]
    public void Запрос_к_отсутствующей_таблице_падает_понятной_ошибкой()
    {
        using var ctx = Context(FrontolSchema.ForTests(ProfileColumns));

        var error = Assert.Throws<InvalidOperationException>(() => ctx.Set<Settings>().ToList());

        Assert.That(error!.Message, Does.Contain(nameof(Settings)));
    }

    [Test]
    public void Отсутствующая_ключевая_колонка_исключает_сущность_из_модели()
    {
        // Ключевой колонке значение по умолчанию не подставишь — таблица для нас бесполезна.
        var withoutId = ProfileColumns.Where(column => column.Column != "ID").ToArray();

        using var ctx = Context(FrontolSchema.ForTests(withoutId));

        Assert.That(ctx.Model.FindEntityType(typeof(Profile)), Is.Null);
    }

    [Test]
    public void Неизвестная_схема_оставляет_модель_как_раньше()
    {
        using var ctx = Context(FrontolSchema.Unknown);
        ctx.UserProfiles = ctx.Set<Profile>();

        Assert.That(ctx.UserProfiles!.ToQueryString(), Does.Contain("FRONTOLSELFIE"));
    }

    [Test]
    public void Модель_не_переиспользуется_для_баз_с_разной_схемой()
    {
        using var oldBase = Context(FrontolSchema.ForTests(
            ProfileColumns.Where(column => column.Column != "FRONTOLSELFIE").ToArray()));

        using var newBase = Context(FrontolSchema.ForTests(ProfileColumns));

        Assert.That(oldBase.Model, Is.Not.SameAs(newBase.Model));
        Assert.That(oldBase.Model.FindEntityType(typeof(Profile))!.FindProperty(nameof(Profile.ForSelfieUser)), Is.Null);
        Assert.That(newBase.Model.FindEntityType(typeof(Profile))!.FindProperty(nameof(Profile.ForSelfieUser)), Is.Not.Null);
    }

    [Test]
    public void Игнорируемое_свойство_остаётся_значением_по_умолчанию()
    {
        var withoutSelfie = ProfileColumns.Where(column => column.Column != "FRONTOLSELFIE").ToArray();

        using var ctx = Context(FrontolSchema.ForTests(withoutSelfie));
        ctx.UserProfiles = ctx.Set<Profile>();

        var profile = new Profile { ForSelfieUser = true, Name = "Кассир" };
        var mapped = ctx.Entry(profile).Properties.Select(property => property.Metadata.Name).ToList();

        Assert.That(mapped, Does.Contain(nameof(Profile.SkipSupervisorMode)));
        Assert.That(mapped, Does.Not.Contain(nameof(Profile.ForSelfieUser)),
            "Свойства без колонки нет в маппинге: в INSERT/UPDATE оно не попадает");

        Assert.That(profile.ForSelfieUser, Is.True,
            "CLR-свойство остаётся доступным и просто не сохраняется в старую базу");
    }

    [Test]
    public void В_реальном_DI_несовместимость_базы_попадает_в_лог()
    {
        var captured = new CapturingLoggerProvider();

        // Отдельный набор колонок, чтобы модель гарантированно строилась заново (а не бралась из кеша EF).
        var schema = FrontolSchema.ForTests(
            ProfileColumns
                .Where(column => column.Column != "FRONTOLSELFIE")
                .Append(("PROFILE", "DI_MARKER"))
                .ToArray());

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(captured));
        services.AddSingleton(schema);
        services.AddDbContext<MainDbCtx>(options => options
            .UseFirebird(ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, FrontolModelCacheKeyFactory>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var ctx = scope.ServiceProvider.GetRequiredService<MainDbCtx>();
        _ = ctx.Model;

        Assert.That(ctx.Schema.IsKnown, Is.True, "Схема должна попадать в контекст через DI");
        Assert.That(captured.Messages, Has.Some.Contains("PROFILE.FRONTOLSELFIE"));
        Assert.That(captured.Messages, Has.Some.Contains("TAXGROUP"));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => messages.Add(formatter(state, exception));
        }
    }
}
