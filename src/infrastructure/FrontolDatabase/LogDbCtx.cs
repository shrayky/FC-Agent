using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;

namespace FrontolDatabase;

public class LogDbCtx : DbContext
{
    public DbSet<LogDb>? Logs { get; set; }
    
    private readonly string _connectionString = string.Empty;
    
    public LogDbCtx(DbContextOptions<LogDbCtx> options)
        : base(options)
    {}

    public LogDbCtx(string connectionString)
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
        
        modelBuilder.Entity<LogDb>()
            .HasKey(k => new { k.Id });
    }
}