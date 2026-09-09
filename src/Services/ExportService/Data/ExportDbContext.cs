using ExportService.Models;
using Microsoft.EntityFrameworkCore;

namespace ExportService.Data;

public sealed class ExportDbContext(DbContextOptions<ExportDbContext> options) : DbContext(options)
{
    public DbSet<ExportRequest> ExportRequests => Set<ExportRequest>();
    public DbSet<ExportFile> ExportFiles => Set<ExportFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExportRequest>(entity =>
        {
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.Property(x => x.ExportType).HasMaxLength(20);
            entity.Property(x => x.Scope).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(30);
            entity.Property(x => x.Period).HasMaxLength(40);
            entity.Property(x => x.RequestedByName).HasMaxLength(200);
            entity.Property(x => x.CriteriaJson).HasMaxLength(2000);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasOne(x => x.File)
                .WithOne(x => x.ExportRequest)
                .HasForeignKey<ExportFile>(x => x.ExportRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExportFile>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.FilePath).HasMaxLength(500);
            entity.Property(x => x.Checksum).HasMaxLength(128);
        });
    }
}
