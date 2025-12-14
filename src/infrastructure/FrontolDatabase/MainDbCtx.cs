using System.Runtime.InteropServices;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;

namespace FrontolDatabase;

public class MainDbCtx : DbContext
{
    public DbSet<CustomDb>? CustomDb { get; set; }
    public DbSet<Settings>? Settings { get; set; }
    public DbSet<Profile>? UserProfiles { get; set; }
    public DbSet<Security>? UserProfileSecurity { get; set; }
    
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
        
        var boolToIntConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<bool, int>(
            v => v ? 1 : 0,
            v => v != 0);
        
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
        
        modelBuilder.Entity<Security>()
            .HasKey(k => new { k.ProfileId, k.SecurityCode });
    }
}