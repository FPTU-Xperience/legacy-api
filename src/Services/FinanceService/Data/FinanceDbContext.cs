using ClubReportHub.Shared.Data;
using FinanceService.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Data;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<BudgetProposal> BudgetProposals => Set<BudgetProposal>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BudgetProposal>(entity =>
        {
            entity.HasIndex(x => new { x.ClubId, x.Status });
            entity.HasIndex(x => x.ProposedAtUtc);
            entity.HasIndex(x => x.ProposedByUserId);
            entity.HasIndex(x => x.SourceReportId).IsUnique().HasFilter("[SourceReportId] IS NOT NULL");
            entity.Property(x => x.ClubName).HasMaxLength(200);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.ManagerReviewNote).HasMaxLength(1000);
            entity.Property(x => x.ReviewNote).HasMaxLength(1000);
            entity.Property(x => x.RequestedAmount).HasPrecision(18, 2);
            entity.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasMany(x => x.Settlements)
                .WithOne(x => x.BudgetProposal)
                .HasForeignKey(x => x.BudgetProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.HasIndex(x => x.BudgetProposalId);
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.TotalSpent).HasPrecision(18, 2);
            entity.Property(x => x.ReceiptUrl).HasMaxLength(500);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.ReviewNote).HasMaxLength(1000);
        });

        modelBuilder.Entity<FinanceTransaction>(entity =>
        {
            entity.HasIndex(x => new { x.ClubId, x.TransactionDateUtc });
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Type).HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000);
        });

        modelBuilder.ApplyOutboxConfiguration();
    }
}
