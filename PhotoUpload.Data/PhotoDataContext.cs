using Microsoft.EntityFrameworkCore;
using PhotoUpload.Data.Models;

namespace PhotoUpload.Data;

public class photoDataContext : DbContext
{
    private readonly string? _connectionString;

    public photoDataContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public photoDataContext(DbContextOptions<photoDataContext> options) : base(options) { }

    public DbSet<Gallery> Galleries => Set<Gallery>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Selection>   Selections   => Set<Selection>();
    public DbSet<PrintOrder>  PrintOrders  => Set<PrintOrder>();
    public DbSet<AdminUser>   AdminUsers   => Set<AdminUser>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var relationship in modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        modelBuilder.Entity<Gallery>(e =>
        {
            e.HasIndex(g => g.UniqueToken).IsUnique();
        });

        modelBuilder.Entity<Selection>(e =>
        {
            e.HasIndex(s => new { s.GalleryId, s.PhotoId }).IsUnique();
        });

        modelBuilder.Entity<Photo>(e =>
        {
            e.HasIndex(p => new { p.GalleryId, p.SortOrder });
        });

        modelBuilder.Entity<AdminUser>(e =>
        {
            e.HasIndex(a => a.Username).IsUnique();
        });
    }
}
