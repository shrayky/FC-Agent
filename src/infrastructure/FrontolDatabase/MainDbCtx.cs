using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
    
    private readonly string _connectionString = string.Empty;   
    
    public MainDbCtx(DbContextOptions<MainDbCtx> options)
        : base(options)
    {}

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

        modelBuilder.Entity<PrintGroup>()
            .HasKey(k => k.Id);
    }

}