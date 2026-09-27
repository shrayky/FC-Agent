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
    public DbSet<CustomDb>? CustomDb { get; set; }
    public DbSet<Settings>? Settings { get; set; }
    public DbSet<Profile>? UserProfiles { get; set; }
    public DbSet<User>? Users { get; set; }
    public DbSet<Security>? UserProfileSecurity { get; set; }
    public DbSet<ActScript>? ActionScripts {  get; set; }
    public DbSet<Devices>? Devices { get; set; }
    public DbSet<Document>? Documents { get; set; }
    public DbSet<DocKind>? DocKinds { get; set; }
    public DbSet<TranzT>? Transactions { get; set; }
    public DbSet<Payment>? Payments { get; set; }
    public DbSet<SprT>? Wares { get; set; }
    public DbSet<PrintGroup>? PrintGroups { get; set; }
    public DbSet<Remain>? Remains { get; set; }
    public DbSet<RemainD>? RemainDs { get; set; }
    public DbSet<PriceData>? PriceDatas { get; set; }
    public DbSet<BarCode>? BarCodes { get; set; }
    public DbSet<TaxGroup>? TaxGroups { get; set; }
    
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