using System.Runtime.InteropServices;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;

namespace FrontolDatabase;

public class MainDbCtx : DbContext
{
    public DbSet<CustomDb>? CustomDb { get; set; }
    public DbSet<Settings>? Settings { get; set; }
    
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
        
        modelBuilder.Entity<CustomDb>()
            .HasKey(k => new { k.Code });
        
        modelBuilder.Entity<Settings>()
            .HasKey(k => new { k.Id });
    }
    
}