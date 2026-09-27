using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase;

public class MainDbCtx : DbContext
{
    private DbSet<CustomDb>? _customDb;
    private DbSet<Settings>? _settings;
    private DbSet<Profile>? _userProfiles;
    private DbSet<User>? _users;
    private DbSet<Security>? _userProfileSecurity;
    private DbSet<ActScript>? _actionScripts;
    private DbSet<Devices>? _devices;
    private DbSet<Document>? _documents;
    private DbSet<DocKind>? _docKinds;
    private DbSet<TranzT>? _transactions;
    private DbSet<Payment>? _payments;
    private DbSet<SprT>? _wares;
    private DbSet<PrintGroup>? _printGroups;
    private DbSet<Remain>? _remains;
    private DbSet<RemainD>? _remainDs;
    private DbSet<PriceData>? _priceDatas;
    private DbSet<BarCode>? _barCodes;
    private DbSet<TaxGroup>? _taxGroups;

    public DbSet<CustomDb>? CustomDb { get => Available(_customDb); set => _customDb = value; }
    public DbSet<Settings>? Settings { get => Available(_settings); set => _settings = value; }
    public DbSet<Profile>? UserProfiles { get => Available(_userProfiles); set => _userProfiles = value; }
    public DbSet<User>? Users { get => Available(_users); set => _users = value; }
    public DbSet<Security>? UserProfileSecurity { get => Available(_userProfileSecurity); set => _userProfileSecurity = value; }
    public DbSet<ActScript>? ActionScripts { get => Available(_actionScripts); set => _actionScripts = value; }
    public DbSet<Devices>? Devices { get => Available(_devices); set => _devices = value; }
    public DbSet<Document>? Documents { get => Available(_documents); set => _documents = value; }
    public DbSet<DocKind>? DocKinds { get => Available(_docKinds); set => _docKinds = value; }
    public DbSet<TranzT>? Transactions { get => Available(_transactions); set => _transactions = value; }
    public DbSet<Payment>? Payments { get => Available(_payments); set => _payments = value; }
    public DbSet<SprT>? Wares { get => Available(_wares); set => _wares = value; }
    public DbSet<PrintGroup>? PrintGroups { get => Available(_printGroups); set => _printGroups = value; }
    public DbSet<Remain>? Remains { get => Available(_remains); set => _remains = value; }
    public DbSet<RemainD>? RemainDs { get => Available(_remainDs); set => _remainDs = value; }
    public DbSet<PriceData>? PriceDatas { get => Available(_priceDatas); set => _priceDatas = value; }
    public DbSet<BarCode>? BarCodes { get => Available(_barCodes); set => _barCodes = value; }
    public DbSet<TaxGroup>? TaxGroups { get => Available(_taxGroups); set => _taxGroups = value; }

    /// <summary>
    /// Сущности, которой нет в подключённой базе (нет таблицы), DbSet не отдаём:
    /// репозитории проверяют свойство на null и вернут понятный отказ вместо обращения
    /// к несуществующей таблице.
    /// </summary>
    private DbSet<TEntity>? Available<TEntity>(DbSet<TEntity>? set) where TEntity : class
        => set is null || Model.FindEntityType(typeof(TEntity)) is not null ? set : null;
    
    private readonly string _connectionString = string.Empty;
    private readonly FrontolSchema _schema = FrontolSchema.Unknown;

    /// <summary>Фактическая схема подключённой базы (таблицы и колонки, которые в ней есть).</summary>
    public FrontolSchema Schema => _schema;

    public MainDbCtx(DbContextOptions<MainDbCtx> options)
        : this(options, FrontolSchema.Unknown)
    {}

    public MainDbCtx(DbContextOptions<MainDbCtx> options, FrontolSchema schema)
        : base(options)
    {
        _schema = schema;
    }

    public MainDbCtx(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        if (!string.IsNullOrEmpty(_connectionString))
            optionsBuilder.UseFirebird(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        var boolToIntConverter = new ValueConverter<bool, int>(
            v => v ? 1 : 0,
            v => v != 0);

        // Frontol TIME: Firebird отдаёт TimeSpan, не DateTime.
        var firebirdTime = new ValueConverter<DateTime, TimeSpan>(
            v => v.TimeOfDay,
            v => DateTime.MinValue.Add(v));
        
        modelBuilder.Entity<CustomDb>()
            .HasKey(k => new { k.Code });
        

        modelBuilder.Entity<Settings>()
            .HasKey(k => new { k.Id });
        

        modelBuilder.Entity<Profile>()
            .HasKey(k => new { k.Id });
        
        modelBuilder.Entity<Profile>()
            .Property(p => p.SkipSupervisorMode)
            .HasConversion(boolToIntConverter);
        
        modelBuilder.Entity<Profile>()
            .Property(p => p.DontChangeUsersOnExchange)
            .HasConversion(boolToIntConverter);
        
        modelBuilder.Entity<Profile>()
            .Property(p => p.ForSelfieUser)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<User>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<Security>()
            .HasKey(k => new { k.ProfileId, k.SecurityCode });


        modelBuilder.Entity<ActScript>()
            .HasKey(k => new { k.Id});

        modelBuilder.Entity<Devices>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<Devices>()
            .Property(p => p.KeepConnetion)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<Devices>()
            .Property(p => p.IsFolder)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<Document>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<DocKind>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<DocKind>()
            .Property(p => p.AskComment)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<DocKind>()
            .Property(p => p.AskEmployee)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<DocKind>()
            .Property(p => p.CopyEmployee)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<DocKind>()
            .Property(p => p.AskCommentOnOpen)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<DocKind>()
            .Property(p => p.Recompense)
            .HasConversion(boolToIntConverter);

        modelBuilder.Entity<Document>()
            .Property(p => p.OpenTime)
            .HasColumnType("TIME")
            .HasConversion(firebirdTime);

        modelBuilder.Entity<Document>()
            .Property(p => p.CloseTime)
            .HasColumnType("TIME")
            .HasConversion(firebirdTime);

        modelBuilder.Entity<TranzT>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<TranzT>()
            .Property(p => p.TranzTime)
            .HasColumnType("TIME")
            .HasConversion(firebirdTime);

        modelBuilder.Entity<TranzT>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<Payment>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<SprT>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<SprT>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<PrintGroup>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<Remain>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<Remain>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<RemainD>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<RemainD>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<PriceData>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<PriceData>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<BarCode>()
            .HasKey(k => k.Id);

        modelBuilder.Entity<BarCode>()
            .Property(p => p.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<TaxGroup>()
            .HasKey(k => k.Id);

        // Обязательно последним: убирает из модели всё, чего нет в подключённой базе.
        ExcludeMissingSchemaParts(modelBuilder);
    }

    /// <summary>
    /// Старая база Frontol может не содержать части таблиц и колонок, которые ожидает модель,
    /// и Firebird падает уже на подготовке запроса ("SQL error code = -206 Column unknown").
    /// Поэтому таблицы, которых нет в базе, тихо исключаются из модели (с записью в лог),
    /// а отсутствующие колонки — из сущности; такое свойство остаётся значением по умолчанию.
    /// </summary>
    private void ExcludeMissingSchemaParts(ModelBuilder modelBuilder)
    {
        var logger = this.GetService<ILoggerFactory>().CreateLogger<MainDbCtx>();

        if (!Schema.IsKnown)
        {
            logger.LogInformation("Схема базы Frontol не определена — модель строится без учёта отсутствующих таблиц и колонок");
            return;
        }

        // Список типов материализуется заранее: дальше модель мутирует (Ignore) прямо во время обхода.
        foreach (var type in ModelEntityTypes(modelBuilder).ToList())
        {
            var entityType = modelBuilder.Entity(type).Metadata;

            if (entityType.GetTableName() is not { } table)
                continue;

            if (!Schema.HasTable(table))
            {
                logger.LogWarning("В базе Frontol нет таблицы {Table} — сущность {Entity} исключена из модели",
                    table, type.Name);

                modelBuilder.Ignore(type);
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());

            var keyColumns = (entityType.FindPrimaryKey()?.Properties ?? [])
                .Select(property => ColumnName(property, storeObject))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var property in entityType.GetProperties().ToList())
            {
                var column = ColumnName(property, storeObject);

                if (Schema.HasColumn(table, column))
                    continue;

                if (keyColumns.Contains(column))
                {
                    logger.LogWarning("В базе Frontol нет ключевой колонки {Table}.{Column} — сущность {Entity} исключена из модели",
                        table, column, type.Name);

                    modelBuilder.Ignore(type);
                    break;
                }

                logger.LogWarning("В базе Frontol нет колонки {Table}.{Column} — свойство {Entity}.{Property} исключено из модели и остаётся значением по умолчанию",
                    table, column, type.Name, property.Name);

                modelBuilder.Entity(type).Ignore(property.Name);
            }
        }
    }

    /// <summary>Типы, которые нужно проверить: уже добавленные в модель и объявленные как DbSet.</summary>
    private IEnumerable<Type> ModelEntityTypes(ModelBuilder modelBuilder)
        => modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .ToList()
            .Concat(GetType().GetProperties()
                .Where(property => property.PropertyType.IsGenericType
                                   && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .Select(property => property.PropertyType.GetGenericArguments()[0]))
            .Distinct();

    private static string ColumnName(IReadOnlyProperty property, StoreObjectIdentifier storeObject)
        => property.GetColumnName(storeObject)
           ?? property.PropertyInfo?.GetCustomAttribute<ColumnAttribute>()?.Name
           ?? property.Name;
}