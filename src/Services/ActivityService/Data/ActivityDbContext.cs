using ActivityService.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivityService.Data;

public sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : DbContext(options)
{
    public DbSet<ClubActivity> Activities => Set<ClubActivity>();
    public DbSet<ActivityParticipant> ActivityParticipants => Set<ActivityParticipant>();
    public DbSet<ActivityAttendance> ActivityAttendances => Set<ActivityAttendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClubActivity>(entity =>
        {
            entity.HasIndex(x => x.SourceReportId).IsUnique().HasFilter("[SourceReportId] IS NOT NULL");
            entity.HasIndex(x => new { x.ClubId, x.StartTimeUtc });
            entity.HasIndex(x => x.StartTimeUtc);
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.ClubName).HasMaxLength(200);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.MeetingDaysCsv).HasMaxLength(32);
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.HasMany(x => x.Participants)
                .WithOne(x => x.Activity)
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Attendances)
                .WithOne(x => x.Activity)
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActivityParticipant>(entity =>
        {
            entity.HasIndex(x => new { x.ActivityId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.Property(x => x.AttendanceStatus).HasMaxLength(40);
        });

        modelBuilder.Entity<ActivityAttendance>(entity =>
        {
            entity.HasIndex(x => new { x.ActivityId, x.UserId, x.AttendanceDate }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.AttendanceDate });
            entity.HasIndex(x => new { x.ActivityId, x.Status });
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.Property(x => x.AttendanceDate).HasColumnType("date");
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.Note).HasMaxLength(1000);
        });
    }
}
