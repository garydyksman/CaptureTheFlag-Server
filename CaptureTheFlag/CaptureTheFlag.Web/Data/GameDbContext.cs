using CaptureTheFlag.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CaptureTheFlag.Web.Data;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameDevice> GameDevices => Set<GameDevice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasMany(e => e.Devices)
                .WithOne(d => d.Game)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerName).HasMaxLength(200);
            entity.Property(e => e.Team).HasMaxLength(32);
            entity.HasIndex(e => new { e.GameId, e.PlayerName }).IsUnique();
        });
    }
}
