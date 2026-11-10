using DBTracker.Core.Entities;
using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure;

public class DBTrackerDbContext : IdentityDbContext<ApplicationUser>
{
    public DBTrackerDbContext(DbContextOptions<DBTrackerDbContext> options) : base(options)
    {
    }

    public DbSet<DatabaseInstance> DatabaseInstances => Set<DatabaseInstance>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<RestoreEvent> RestoreEvents => Set<RestoreEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceLinkToken> DeviceLinkTokens => Set<DeviceLinkToken>();
    public DbSet<InstanceChangeLog> InstanceChangeLogs => Set<InstanceChangeLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DatabaseInstance>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Version).HasMaxLength(50);
            entity.Property(e => e.Application).HasMaxLength(200);
            entity.Property(e => e.Company).HasMaxLength(200);
            entity.Property(e => e.Department).HasMaxLength(200);
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.ContactEmail).HasMaxLength(320);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
            entity.Property(e => e.DatabaseIp).HasMaxLength(100);
            entity.Property(e => e.ServerIp).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.BackupFileLocation).HasMaxLength(500);
            entity.Property(e => e.LastScanError).HasMaxLength(1000);
            entity.Property(e => e.Tags).HasMaxLength(300);
            entity.Property(e => e.RunbookUrl).HasMaxLength(500);
            entity.Property(e => e.MaintenanceReason).HasMaxLength(500);

            entity.HasIndex(e => e.Application);
            entity.HasIndex(e => e.Company);
            entity.HasIndex(e => e.Engine);
        });

        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.SourceFile).HasMaxLength(260);

            entity.HasOne(r => r.DatabaseInstance)
                .WithMany(i => i.BackupRecords)
                .HasForeignKey(r => r.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.TimestampUtc });
        });

        modelBuilder.Entity<RestoreEvent>(entity =>
        {
            entity.Property(e => e.PerformedBy).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(2000);

            entity.HasOne(r => r.DatabaseInstance)
                .WithMany(i => i.RestoreEvents)
                .HasForeignKey(r => r.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.TimestampUtc });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.InstanceName).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(500);

            entity.HasOne(n => n.DatabaseInstance)
                .WithMany()
                .HasForeignKey(n => n.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => e.IsRead);
        });

        modelBuilder.Entity<DeviceLinkToken>(entity =>
        {
            entity.Property(e => e.Token).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ApprovedByUserId).HasMaxLength(450);

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.ExpiresAtUtc);
        });

        modelBuilder.Entity<InstanceChangeLog>(entity =>
        {
            entity.Property(e => e.ChangedBy).HasMaxLength(320);
            entity.Property(e => e.FieldName).HasMaxLength(100);
            entity.Property(e => e.OldValue).HasMaxLength(2000);
            entity.Property(e => e.NewValue).HasMaxLength(2000);

            entity.HasOne(c => c.DatabaseInstance)
                .WithMany()
                .HasForeignKey(c => c.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.ChangedAtUtc });
        });
    }
}
