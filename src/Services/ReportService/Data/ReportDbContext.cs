using ClubReportHub.Shared.Data;
using Microsoft.EntityFrameworkCore;
using ReportService.Models;

namespace ReportService.Data;

public sealed class ReportDbContext(DbContextOptions<ReportDbContext> options) : DbContext(options)
{
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportDetail> ReportDetails => Set<ReportDetail>();
    public DbSet<ReportAttachment> ReportAttachments => Set<ReportAttachment>();
    public DbSet<ReportUploadedFile> ReportUploadedFiles => Set<ReportUploadedFile>();
    public DbSet<ReportFeedback> ReportFeedback => Set<ReportFeedback>();
    public DbSet<ReportingDeadline> ReportingDeadlines => Set<ReportingDeadline>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasIndex(x => new { x.ClubId, x.Period, x.Tag });
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.UpdatedAtUtc);
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasIndex(x => x.BudgetProposalId).IsUnique().HasFilter("[BudgetProposalId] IS NOT NULL");

            // SeedKey: nullable varchar(50), filtered unique index to prevent duplicate demo reports
            entity.Property(x => x.SeedKey).HasMaxLength(50).IsUnicode(false);
            entity.HasIndex(x => x.SeedKey).IsUnique().HasFilter("[SeedKey] IS NOT NULL");

            entity.Property(x => x.ClubName).HasMaxLength(200);
            entity.Property(x => x.Period).HasMaxLength(40);
            entity.Property(x => x.ReportType).HasMaxLength(80);
            entity.Property(x => x.Tag).HasMaxLength(80);
            entity.Property(x => x.ContentSource).HasMaxLength(40).HasDefaultValue(ReportContentSources.StructuredForm);
            entity.Property(x => x.BudgetDescription).HasMaxLength(2000);
            entity.Property(x => x.BudgetRequestedAmount).HasPrecision(18, 2);
            entity.Property(x => x.BudgetApprovedAmount).HasPrecision(18, 2);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.Details).WithOne(x => x.Report).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Attachments).WithOne(x => x.Report).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UploadedFile).WithOne(x => x.Report).HasForeignKey<ReportUploadedFile>(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Feedback).WithOne(x => x.Report).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportUploadedFile>(entity =>
        {
            entity.HasIndex(x => x.ReportId).IsUnique();
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.StoredFileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.FileExtension).HasMaxLength(10);
            entity.Property(x => x.StoragePath).HasMaxLength(500);
            entity.Property(x => x.Checksum).HasMaxLength(64);
            entity.Property(x => x.PreviewFileName).HasMaxLength(260);
            entity.Property(x => x.PreviewStoragePath).HasMaxLength(500);
            entity.Property(x => x.PreviewContentType).HasMaxLength(120);
            entity.Property(x => x.PreviewStatus).HasMaxLength(40);
            entity.Property(x => x.PreviewErrorMessage).HasMaxLength(1000);
        });

        modelBuilder.Entity<ReportDetail>(entity =>
        {
            entity.Property(x => x.ActivityName).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Outcome).HasMaxLength(1000);
            entity.Property(x => x.ActivityType).HasMaxLength(100);
            entity.Property(x => x.Location).HasMaxLength(250);
            entity.Property(x => x.PartnerUnit).HasMaxLength(250);
            entity.Property(x => x.EvidenceUrl).HasMaxLength(1000);
            entity.Property(x => x.BudgetSpent).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ReportAttachment>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.StoragePath).HasMaxLength(500);
        });

        modelBuilder.Entity<ReportFeedback>(entity =>
        {
            entity.Property(x => x.ReviewerName).HasMaxLength(200);
            entity.Property(x => x.Decision).HasMaxLength(40);
            entity.Property(x => x.Message).HasMaxLength(2000);
        });

        modelBuilder.Entity<ReportingDeadline>(entity =>
        {
            entity.HasIndex(x => x.Period).IsUnique();
            entity.Property(x => x.Period).HasMaxLength(40);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.ReportId);
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000);
        });

        modelBuilder.ApplyOutboxConfiguration();
    }
}
