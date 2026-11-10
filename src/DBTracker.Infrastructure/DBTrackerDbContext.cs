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
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<NotificationReadState> NotificationReadStates => Set<NotificationReadState>();
    public DbSet<NotificationSubscription> NotificationSubscriptions => Set<NotificationSubscription>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<BackupRecordCorrection> BackupRecordCorrections => Set<BackupRecordCorrection>();
    public DbSet<RestoreEventCorrection> RestoreEventCorrections => Set<RestoreEventCorrection>();
    public DbSet<ComplianceSnapshot> ComplianceSnapshots => Set<ComplianceSnapshot>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<BackupPolicy> BackupPolicies => Set<BackupPolicy>();
    public DbSet<ContactCorrectionRequest> ContactCorrectionRequests => Set<ContactCorrectionRequest>();

    // Epic 18 (D01-D06): database identity, releases and support lifecycle.
    public DbSet<VersionIdentity> VersionIdentities => Set<VersionIdentity>();
    public DbSet<ClusterNodeVersion> ClusterNodeVersions => Set<ClusterNodeVersion>();
    public DbSet<ReleaseCatalogEntry> ReleaseCatalogEntries => Set<ReleaseCatalogEntry>();
    public DbSet<ReleaseCatalogRevision> ReleaseCatalogRevisions => Set<ReleaseCatalogRevision>();
    public DbSet<ApprovedPatchBaseline> ApprovedPatchBaselines => Set<ApprovedPatchBaseline>();
    public DbSet<SupportRunwayDecision> SupportRunwayDecisions => Set<SupportRunwayDecision>();
    public DbSet<AdvisoryRecord> AdvisoryRecords => Set<AdvisoryRecord>();
    public DbSet<AdvisoryInstanceAssessment> AdvisoryInstanceAssessments => Set<AdvisoryInstanceAssessment>();
    public DbSet<VersionObservation> VersionObservations => Set<VersionObservation>();

    // Epic 19 (D07-D10): dependency assessment and migration planning.
    public DbSet<InstanceDependency> InstanceDependencies => Set<InstanceDependency>();
    public DbSet<PathSupportRule> PathSupportRules => Set<PathSupportRule>();
    public DbSet<PathAssessment> PathAssessments => Set<PathAssessment>();
    public DbSet<ReadinessChecklist> ReadinessChecklists => Set<ReadinessChecklist>();
    public DbSet<ReadinessCheckItem> ReadinessCheckItems => Set<ReadinessCheckItem>();
    public DbSet<MigrationCampaign> MigrationCampaigns => Set<MigrationCampaign>();
    public DbSet<CampaignInstanceLink> CampaignInstanceLinks => Set<CampaignInstanceLink>();
    public DbSet<CampaignAlternative> CampaignAlternatives => Set<CampaignAlternative>();
    public DbSet<CampaignStateTransition> CampaignStateTransitions => Set<CampaignStateTransition>();
    public DbSet<CampaignApproval> CampaignApprovals => Set<CampaignApproval>();

    // Epic 20 (D11-D15): rehearsal/recovery evidence, scheduled change windows and cutover journal,
    // post-change validation, generic readiness exceptions, and the lifecycle workspace (read-only
    // over existing data — no new DbSet needed for D15 itself).
    public DbSet<RehearsalRecord> RehearsalRecords => Set<RehearsalRecord>();
    public DbSet<RehearsalRollbackDecisionPoint> RehearsalRollbackDecisionPoints => Set<RehearsalRollbackDecisionPoint>();
    public DbSet<ChangeWindow> ChangeWindows => Set<ChangeWindow>();
    public DbSet<ChangeWindowRunbookStep> ChangeWindowRunbookSteps => Set<ChangeWindowRunbookStep>();
    public DbSet<ChangeWindowApproval> ChangeWindowApprovals => Set<ChangeWindowApproval>();
    public DbSet<CutoverJournalEntry> CutoverJournalEntries => Set<CutoverJournalEntry>();
    public DbSet<ChangeValidation> ChangeValidations => Set<ChangeValidation>();
    public DbSet<ChangeValidationSignoff> ChangeValidationSignoffs => Set<ChangeValidationSignoff>();
    public DbSet<ReadinessException> ReadinessExceptions => Set<ReadinessException>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            // Every existing user (including seed data from before U16) must stay able to sign in
            // once this column exists — the SQL-side default has to be true, not bool's CLR default
            // of false, or the migration would retroactively lock out every account that already exists.
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

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
            entity.Property(e => e.BackupFileNamePatterns).HasMaxLength(300);
            entity.Property(e => e.LastScanError).HasMaxLength(1000);
            entity.Property(e => e.Tags).HasMaxLength(300);
            entity.Property(e => e.RunbookUrl).HasMaxLength(500);
            entity.Property(e => e.MaintenanceReason).HasMaxLength(500);
            entity.Property(e => e.ArchivedBy).HasMaxLength(320);
            entity.Property(e => e.ArchiveReason).HasMaxLength(500);
            entity.Property(e => e.ConnectivityHostOverride).HasMaxLength(255);
            entity.Property(e => e.RowVersion).IsRowVersion();

            // U21 additions.
            entity.Property(e => e.BackupContactName).HasMaxLength(200);
            entity.Property(e => e.BackupContactEmail).HasMaxLength(320);
            entity.Property(e => e.BackupContactPhone).HasMaxLength(50);
            entity.Property(e => e.OwnershipConfirmedBy).HasMaxLength(320);
            entity.Property(e => e.Region).HasMaxLength(100);
            entity.Property(e => e.Cluster).HasMaxLength(100);
            entity.Property(e => e.ReplicaSet).HasMaxLength(100);

            entity.HasOne(e => e.OwnerTeam)
                .WithMany()
                .HasForeignKey(e => e.OwnerTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.AppliedPolicy)
                .WithMany()
                .HasForeignKey(e => e.AppliedPolicyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Application);
            entity.HasIndex(e => e.Company);
            entity.HasIndex(e => e.Engine);
            entity.HasIndex(e => e.IsArchived);
            entity.HasIndex(e => e.OwnerTeamId);
            entity.HasIndex(e => e.Region);
            entity.HasIndex(e => e.Cluster);
            entity.HasIndex(e => e.Criticality);
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

            // Guards against duplicate Discovered evidence from two concurrent scans racing each
            // other (U01): the filter only applies to rows with a known byte signature, so legacy
            // Discovered records (ObservedFileBytes null, predating this column) aren't constrained.
            entity.HasIndex(e => new { e.DatabaseInstanceId, e.SourceFile, e.ObservedFileBytes })
                .IsUnique()
                .HasFilter("[Source] = 1 AND [ObservedFileBytes] IS NOT NULL");
        });

        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.Property(e => e.RecordedByUserName).HasMaxLength(320);
        });

        modelBuilder.Entity<RestoreEvent>(entity =>
        {
            entity.Property(e => e.PerformedBy).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.RecordedByUserName).HasMaxLength(320);

            entity.HasOne(r => r.DatabaseInstance)
                .WithMany(i => i.RestoreEvents)
                .HasForeignKey(r => r.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction, not SetNull: SQL Server rejects a second cascade path to BackupRecords
            // (DatabaseInstance already cascades to both BackupRecords and RestoreEvents). A
            // backup record referenced by a restore test is never deleted by itself anyway —
            // only alongside its whole instance, which cascades RestoreEvents away first.
            entity.HasOne(r => r.LinkedBackupRecord)
                .WithMany()
                .HasForeignKey(r => r.LinkedBackupRecordId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.TimestampUtc });
        });

        modelBuilder.Entity<BackupRecordCorrection>(entity =>
        {
            entity.Property(e => e.CorrectedByUserName).HasMaxLength(320);
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.PreviousNotes).HasMaxLength(2000);
            entity.Property(e => e.NewNotes).HasMaxLength(2000);

            entity.HasOne(c => c.BackupRecord)
                .WithMany(r => r.Corrections)
                .HasForeignKey(c => c.BackupRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.BackupRecordId);
        });

        modelBuilder.Entity<RestoreEventCorrection>(entity =>
        {
            entity.Property(e => e.CorrectedByUserName).HasMaxLength(320);
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.PreviousNotes).HasMaxLength(2000);
            entity.Property(e => e.NewNotes).HasMaxLength(2000);

            entity.HasOne(c => c.RestoreEvent)
                .WithMany(r => r.Corrections)
                .HasForeignKey(c => c.RestoreEventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.RestoreEventId);
        });

        modelBuilder.Entity<ComplianceSnapshot>(entity =>
        {
            entity.Property(e => e.GeneratedByUserName).HasMaxLength(320);
            entity.Property(e => e.ScopeDescription).HasMaxLength(500);
            entity.Property(e => e.PolicyVersion).HasMaxLength(50);
            entity.Property(e => e.EvidenceProvenance).HasMaxLength(500);
            entity.Property(e => e.RowsJson).HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.GeneratedAtUtc);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.InstanceName).HasMaxLength(200);
            entity.Property(e => e.Company).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(500);

            entity.HasOne(n => n.DatabaseInstance)
                .WithMany()
                .HasForeignKey(n => n.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => e.IsRead);
            entity.HasIndex(e => e.OwnerTeamId);
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

        modelBuilder.Entity<SavedView>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.ViewType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.QueryString).HasMaxLength(1000);

            entity.HasIndex(e => new { e.UserId, e.ViewType });
            // One user can't have two saved views with the same name for the same page.
            entity.HasIndex(e => new { e.UserId, e.ViewType, e.Name }).IsUnique();
        });

        modelBuilder.Entity<NotificationReadState>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();

            entity.HasOne(e => e.Notification)
                .WithMany()
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            // A notification can only be read once per user — upserts rely on this to stay idempotent.
            entity.HasIndex(e => new { e.NotificationId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.Company).HasMaxLength(200);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Team)
                .WithMany()
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PrimaryContactName).HasMaxLength(200);
            entity.Property(e => e.PrimaryContactEmail).HasMaxLength(320);
            entity.Property(e => e.BackupContactName).HasMaxLength(200);
            entity.Property(e => e.BackupContactEmail).HasMaxLength(320);

            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<TeamMembership>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();

            entity.HasOne(e => e.Team)
                .WithMany(t => t.Memberships)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            // A user can't be added to the same team twice.
            entity.HasIndex(e => new { e.TeamId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<BackupPolicy>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(320);

            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ContactCorrectionRequest>(entity =>
        {
            entity.Property(e => e.RequestedByUserId).HasMaxLength(450);
            entity.Property(e => e.RequestedByName).HasMaxLength(320);
            entity.Property(e => e.ProposedContactName).HasMaxLength(200);
            entity.Property(e => e.ProposedContactEmail).HasMaxLength(320);
            entity.Property(e => e.ProposedContactPhone).HasMaxLength(50);
            entity.Property(e => e.ProposedBackupContactName).HasMaxLength(200);
            entity.Property(e => e.ProposedBackupContactEmail).HasMaxLength(320);
            entity.Property(e => e.ProposedBackupContactPhone).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.ReviewedByName).HasMaxLength(320);
            entity.Property(e => e.ReviewNote).HasMaxLength(1000);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.Status });
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.Property(e => e.AssigneeName).HasMaxLength(200);
            entity.Property(e => e.AcknowledgedBy).HasMaxLength(200);
            entity.Property(e => e.Note).HasMaxLength(2000);
            entity.Property(e => e.TicketReference).HasMaxLength(200);
            entity.Ignore(e => e.IsOpen);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // "Is there already an open incident for this instance" is checked on every alert
            // sweep, so it needs to be cheap; ClosedAtUtc == null identifies the open one(s).
            entity.HasIndex(e => new { e.DatabaseInstanceId, e.ClosedAtUtc });
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.Property(e => e.ActorUserId).HasMaxLength(450);
            entity.Property(e => e.ActorName).HasMaxLength(320);
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(100);
            entity.Property(e => e.TargetId).HasMaxLength(100);
            entity.Property(e => e.TargetName).HasMaxLength(320);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.BeforeJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.AfterJson).HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => new { e.TargetType, e.TargetId });
        });

        // Epic 18 (D01-D06).
        modelBuilder.Entity<VersionIdentity>(entity =>
        {
            entity.Property(e => e.RawVersionText).HasMaxLength(200);
            entity.Property(e => e.UnresolvedReason).HasMaxLength(500);
            entity.Property(e => e.Family).HasMaxLength(100);
            entity.Property(e => e.Build).HasMaxLength(100);
            entity.Property(e => e.Edition).HasMaxLength(100);
            entity.Property(e => e.Distribution).HasMaxLength(100);
            entity.Property(e => e.PatchTrain).HasMaxLength(100);
            entity.Property(e => e.HostingProvider).HasMaxLength(200);
            entity.Property(e => e.HostingContext).HasMaxLength(500);
            entity.Property(e => e.HostingEngineVersion).HasMaxLength(100);
            entity.Property(e => e.DatabaseCompatibilityLevel).HasMaxLength(100);
            entity.Property(e => e.AttestedBy).HasMaxLength(320);
            entity.Property(e => e.UpdatedBy).HasMaxLength(320);

            entity.HasOne(e => e.DatabaseInstance)
                .WithOne()
                .HasForeignKey<VersionIdentity>(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.DatabaseInstanceId).IsUnique();
            entity.HasIndex(e => e.ResolutionStatus);
            entity.HasIndex(e => new { e.Family, e.Major, e.Minor });
        });

        modelBuilder.Entity<ClusterNodeVersion>(entity =>
        {
            entity.Property(e => e.NodeName).HasMaxLength(200);
            entity.Property(e => e.RawVersionText).HasMaxLength(200);
            entity.Property(e => e.Family).HasMaxLength(100);
            entity.Property(e => e.Build).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction, not SetNull: SQL Server rejects a second cascade path to ClusterNodeVersions
            // (DatabaseInstance already cascades to both VersionIdentities and ClusterNodeVersions
            // directly). A node row is never deleted on its own anyway — only alongside its whole
            // instance, which cascades it away regardless of this FK's own action.
            entity.HasOne(e => e.VersionIdentity)
                .WithMany(v => v.ClusterNodeVersions)
                .HasForeignKey(e => e.VersionIdentityId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.DatabaseInstanceId);
        });

        modelBuilder.Entity<ReleaseCatalogEntry>(entity =>
        {
            entity.Property(e => e.Family).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ReleaseLabel).HasMaxLength(200);
            entity.Property(e => e.Build).HasMaxLength(100);
            entity.Property(e => e.PatchTrain).HasMaxLength(100);
            entity.Property(e => e.Edition).HasMaxLength(100);
            entity.Property(e => e.Distribution).HasMaxLength(100);
            entity.Property(e => e.ApplicabilityNotes).HasMaxLength(1000);
            entity.Property(e => e.DateTimezone).HasMaxLength(50);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.SourceReference).HasMaxLength(500);
            entity.Property(e => e.ReviewedBy).HasMaxLength(320);
            entity.Property(e => e.OverrideReason).HasMaxLength(1000);
            entity.Property(e => e.OverrideReviewedBy).HasMaxLength(320);
            entity.Property(e => e.LastRefreshError).HasMaxLength(1000);
            entity.Property(e => e.UpdatedBy).HasMaxLength(320);

            entity.HasIndex(e => new { e.Engine, e.Family, e.Major, e.Minor });
        });

        modelBuilder.Entity<ReleaseCatalogRevision>(entity =>
        {
            entity.Property(e => e.RevisedBy).HasMaxLength(320);
            entity.Property(e => e.ChangeSummary).HasMaxLength(500);
            entity.Property(e => e.SnapshotJson).HasColumnType("nvarchar(max)");

            entity.HasOne(e => e.ReleaseCatalogEntry)
                .WithMany(c => c.Revisions)
                .HasForeignKey(e => e.ReleaseCatalogEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ReleaseCatalogEntryId);
        });

        modelBuilder.Entity<ApprovedPatchBaseline>(entity =>
        {
            entity.Property(e => e.Family).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ReviewedBy).HasMaxLength(320);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.TargetReleaseCatalogEntry)
                .WithMany()
                .HasForeignKey(e => e.TargetReleaseCatalogEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.Engine, e.Family, e.IsActive });
        });

        modelBuilder.Entity<SupportRunwayDecision>(entity =>
        {
            entity.Property(e => e.ActionReference).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.DecidedBy).HasMaxLength(320);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ReleaseCatalogEntry)
                .WithMany()
                .HasForeignKey(e => e.ReleaseCatalogEntryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.DecidedAtUtc });
        });

        modelBuilder.Entity<AdvisoryRecord>(entity =>
        {
            entity.Property(e => e.Family).HasMaxLength(100).IsRequired();
            entity.Property(e => e.VendorAdvisoryId).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.AffectedMinBuild).HasMaxLength(100);
            entity.Property(e => e.AffectedMaxBuild).HasMaxLength(100);
            entity.Property(e => e.AffectedConfiguration).HasMaxLength(500);
            entity.Property(e => e.FixedBuild).HasMaxLength(100);
            entity.Property(e => e.ProvenanceUrl).HasMaxLength(500);
            entity.Property(e => e.ImportedBy).HasMaxLength(320);

            entity.HasIndex(e => new { e.Engine, e.Family });
            entity.HasIndex(e => e.VendorAdvisoryId);
        });

        modelBuilder.Entity<AdvisoryInstanceAssessment>(entity =>
        {
            entity.Property(e => e.ClassificationReasoning).HasMaxLength(1000);
            entity.Property(e => e.ManagedServiceResponsibility).HasMaxLength(500);
            entity.Property(e => e.ReviewerEvidence).HasMaxLength(1000);
            entity.Property(e => e.ReviewedBy).HasMaxLength(320);
            entity.Property(e => e.Owner).HasMaxLength(320);
            entity.Property(e => e.TargetPatch).HasMaxLength(200);
            entity.Property(e => e.VerificationNotes).HasMaxLength(1000);

            entity.HasOne(e => e.AdvisoryRecord)
                .WithMany(a => a.Assessments)
                .HasForeignKey(e => e.AdvisoryRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.AdvisoryRecordId, e.DatabaseInstanceId }).IsUnique();
            entity.HasIndex(e => e.DatabaseInstanceId);
        });

        modelBuilder.Entity<VersionObservation>(entity =>
        {
            entity.Property(e => e.RawVersionText).HasMaxLength(200);
            entity.Property(e => e.FailureDetail).HasMaxLength(500);
            entity.Property(e => e.ObservedBy).HasMaxLength(320);
            entity.Property(e => e.DriftDetail).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.ObservedAtUtc });
        });

        // Epic 19 (D07-D10).
        modelBuilder.Entity<InstanceDependency>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.RecordedVersionText).HasMaxLength(100);
            entity.Property(e => e.OsRuntimeConstraint).HasMaxLength(500);
            entity.Property(e => e.ManagedProviderConstraint).HasMaxLength(500);
            entity.Property(e => e.EvidenceUrl).HasMaxLength(500);
            entity.Property(e => e.EvidenceNotes).HasMaxLength(1000);
            entity.Property(e => e.ResolutionNotes).HasMaxLength(1000);
            entity.Property(e => e.LastReviewedBy).HasMaxLength(320);
            entity.Property(e => e.UpdatedBy).HasMaxLength(320);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.OwnerTeam)
                .WithMany()
                .HasForeignKey(e => e.OwnerTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.IsResolved });
            entity.HasIndex(e => e.Direction);
        });

        modelBuilder.Entity<PathSupportRule>(entity =>
        {
            entity.Property(e => e.SourceFamily).HasMaxLength(100);
            entity.Property(e => e.SourceBuildMin).HasMaxLength(100);
            entity.Property(e => e.SourceBuildMax).HasMaxLength(100);
            entity.Property(e => e.SourceEdition).HasMaxLength(100);
            entity.Property(e => e.SourcePlatform).HasMaxLength(100);
            entity.Property(e => e.SourceTopology).HasMaxLength(100);
            entity.Property(e => e.TargetFamily).HasMaxLength(100);
            entity.Property(e => e.TargetBuildMin).HasMaxLength(100);
            entity.Property(e => e.TargetBuildMax).HasMaxLength(100);
            entity.Property(e => e.TargetEdition).HasMaxLength(100);
            entity.Property(e => e.TargetPlatform).HasMaxLength(100);
            entity.Property(e => e.TargetTopology).HasMaxLength(100);
            entity.Property(e => e.RequiredIntermediateVersions).HasMaxLength(500);
            entity.Property(e => e.DowngradeConstraints).HasMaxLength(500);
            entity.Property(e => e.VendorGuidanceUrl).HasMaxLength(500);
            entity.Property(e => e.VendorGuidanceNotes).HasMaxLength(1000);
            entity.Property(e => e.ReviewedBy).HasMaxLength(320);

            entity.HasIndex(e => new { e.Engine, e.AssessmentType, e.Method });
        });

        modelBuilder.Entity<PathAssessment>(entity =>
        {
            entity.Property(e => e.SourceProduct).HasMaxLength(200);
            entity.Property(e => e.SourceBuild).HasMaxLength(100);
            entity.Property(e => e.SourceEdition).HasMaxLength(100);
            entity.Property(e => e.SourcePlatform).HasMaxLength(100);
            entity.Property(e => e.SourceTopology).HasMaxLength(100);
            entity.Property(e => e.TargetProduct).HasMaxLength(200);
            entity.Property(e => e.TargetBuild).HasMaxLength(100);
            entity.Property(e => e.TargetEdition).HasMaxLength(100);
            entity.Property(e => e.TargetPlatform).HasMaxLength(100);
            entity.Property(e => e.TargetTopology).HasMaxLength(100);
            entity.Property(e => e.StatusReasoning).HasMaxLength(1000);
            entity.Property(e => e.VendorGuidanceUrl).HasMaxLength(500);
            entity.Property(e => e.VendorGuidanceNotes).HasMaxLength(1000);
            entity.Property(e => e.RequiredIntermediateVersions).HasMaxLength(500);
            entity.Property(e => e.DowngradeConstraints).HasMaxLength(500);
            entity.Property(e => e.EvaluatedBy).HasMaxLength(320);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MatchedRule)
                .WithMany()
                .HasForeignKey(e => e.MatchedRuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.EvaluatedAtUtc });
        });

        modelBuilder.Entity<ReadinessChecklist>(entity =>
        {
            entity.Property(e => e.SourceLabel).HasMaxLength(300);
            entity.Property(e => e.TargetLabel).HasMaxLength(300);
            entity.Property(e => e.CreatedBy).HasMaxLength(320);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction, not Cascade: SQL Server rejects a second cascade path to ReadinessChecklist
            // (DatabaseInstance already cascades to it directly, and also to PathAssessment, which
            // this FK also points at). A checklist is never deleted on its own via this FK anyway —
            // only alongside its whole instance, which cascades it away regardless of this FK's own
            // action. Same resolution already used for ClusterNodeVersion.VersionIdentityId and
            // RestoreEvent.LinkedBackupRecordId.
            entity.HasOne(e => e.PathAssessment)
                .WithMany()
                .HasForeignKey(e => e.PathAssessmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.DatabaseInstanceId);
        });

        modelBuilder.Entity<ReadinessCheckItem>(entity =>
        {
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.Findings).HasMaxLength(2000);
            entity.Property(e => e.Reviewer).HasMaxLength(320);
            entity.Property(e => e.SourceReference).HasMaxLength(500);
            entity.Property(e => e.TargetReference).HasMaxLength(500);
            entity.Property(e => e.RemediationOwner).HasMaxLength(320);
            entity.Property(e => e.RemediationNotes).HasMaxLength(1000);
            entity.Property(e => e.InvalidationReason).HasMaxLength(500);

            entity.HasOne(e => e.ReadinessChecklist)
                .WithMany(c => c.Items)
                .HasForeignKey(e => e.ReadinessChecklistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ReadinessChecklistId);
        });

        modelBuilder.Entity<MigrationCampaign>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SourceLabel).HasMaxLength(300);
            entity.Property(e => e.TargetLabel).HasMaxLength(300);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Blockers).HasMaxLength(1000);
            entity.Property(e => e.Milestones).HasMaxLength(2000);
            entity.Property(e => e.ChangeTicketReference).HasMaxLength(200);
            entity.Property(e => e.CreatedBy).HasMaxLength(320);
            entity.Property(e => e.EstimatedEffortHours).HasColumnType("decimal(10,2)");
            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(14,2)");

            // NoAction on all three, not SetNull: three separate FK columns on one table all
            // referencing the same Teams table hits the same SQL Server restriction as a single
            // table reachable via two cascade paths ("may cause cycles or multiple cascade paths"),
            // even though these three don't chain through each other. There is no team-delete
            // feature in this app (TeamService has no delete/remove-team method) so this never
            // blocks a real operation; same resolution already used elsewhere in this schema for an
            // identical shape (RestoreEvent.LinkedBackupRecord, ClusterNodeVersion.VersionIdentity).
            entity.HasOne(e => e.AccountableDbaTeam)
                .WithMany()
                .HasForeignKey(e => e.AccountableDbaTeamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.AccountableAppTeam)
                .WithMany()
                .HasForeignKey(e => e.AccountableAppTeamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.AccountablePlatformTeam)
                .WithMany()
                .HasForeignKey(e => e.AccountablePlatformTeamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.State);
        });

        modelBuilder.Entity<CampaignInstanceLink>(entity =>
        {
            entity.HasOne(e => e.MigrationCampaign)
                .WithMany(c => c.InstanceLinks)
                .HasForeignKey(e => e.MigrationCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MigrationCampaignId, e.DatabaseInstanceId }).IsUnique();
            entity.HasIndex(e => e.DatabaseInstanceId);
        });

        modelBuilder.Entity<CampaignAlternative>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.RiskNotes).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(320);
            entity.Property(e => e.EstimatedEffortHours).HasColumnType("decimal(10,2)");
            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(14,2)");

            entity.HasOne(e => e.MigrationCampaign)
                .WithMany(c => c.Alternatives)
                .HasForeignKey(e => e.MigrationCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.MigrationCampaignId);
        });

        modelBuilder.Entity<CampaignStateTransition>(entity =>
        {
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.TransitionedBy).HasMaxLength(320);

            entity.HasOne(e => e.MigrationCampaign)
                .WithMany(c => c.StateTransitions)
                .HasForeignKey(e => e.MigrationCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MigrationCampaignId, e.TransitionedAtUtc });
        });

        modelBuilder.Entity<CampaignApproval>(entity =>
        {
            entity.Property(e => e.Scope).HasMaxLength(200);
            entity.Property(e => e.ApproverName).HasMaxLength(320);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.MigrationCampaign)
                .WithMany(c => c.Approvals)
                .HasForeignKey(e => e.MigrationCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.MigrationCampaignId);
        });

        // Epic 20 (D11-D15).
        modelBuilder.Entity<RehearsalRecord>(entity =>
        {
            entity.Property(e => e.SourceLabel).HasMaxLength(300);
            entity.Property(e => e.TargetLabel).HasMaxLength(300);
            entity.Property(e => e.RepresentativeDatasetDescription).HasMaxLength(1000);
            entity.Property(e => e.PerformedBy).HasMaxLength(200);
            entity.Property(e => e.CorrectnessCheckNotes).HasMaxLength(2000);
            entity.Property(e => e.PerformanceComparisonNotes).HasMaxLength(2000);
            entity.Property(e => e.PrerequisitesText).HasMaxLength(2000);
            entity.Property(e => e.RecoveryKeyDependenciesText).HasMaxLength(2000);
            entity.Property(e => e.PostCutoverWriteReconciliationPlan).HasMaxLength(2000);
            entity.Property(e => e.RecordedBy).HasMaxLength(320);

            entity.HasOne(e => e.MigrationCampaign)
                .WithMany()
                .HasForeignKey(e => e.MigrationCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction, not Cascade/SetNull: DatabaseInstance already cascades to this table directly
            // and to PathAssessment, which this FK also points at — the same "second cascade path"
            // shape already resolved this way for ReadinessChecklist.PathAssessmentId.
            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.PathAssessment)
                .WithMany()
                .HasForeignKey(e => e.PathAssessmentId)
                .OnDelete(DeleteBehavior.NoAction);

            // NoAction, not SetNull: same shape as RestoreEvent.LinkedBackupRecordId — a restore
            // event referenced by a rehearsal is never deleted on its own, only alongside its whole
            // instance, which already cascades RestoreEvents away.
            entity.HasOne(e => e.LinkedRestoreEvent)
                .WithMany()
                .HasForeignKey(e => e.LinkedRestoreEventId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.PerformedAtUtc });
            entity.HasIndex(e => e.MigrationCampaignId);
        });

        modelBuilder.Entity<RehearsalRollbackDecisionPoint>(entity =>
        {
            entity.Property(e => e.TriggerCondition).HasMaxLength(500);
            entity.Property(e => e.Decision).HasMaxLength(1000);

            entity.HasOne(e => e.RehearsalRecord)
                .WithMany(r => r.RollbackDecisionPoints)
                .HasForeignKey(e => e.RehearsalRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.RehearsalRecordId);
        });

        modelBuilder.Entity<ChangeWindow>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.TimeZoneLabel).HasMaxLength(100);
            entity.Property(e => e.RunbookNotes).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).HasMaxLength(320);

            entity.HasOne(e => e.MigrationCampaign)
                .WithMany()
                .HasForeignKey(e => e.MigrationCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CommunicationOwnerTeam)
                .WithMany()
                .HasForeignKey(e => e.CommunicationOwnerTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            // NoAction: ReadinessException is also reachable from MigrationCampaign/DatabaseInstance
            // via its generic TargetType/TargetId (no FK, so no cascade path there), but this is a
            // direct FK from ChangeWindow — Restrict/NoAction avoids any future cycle if a cascade
            // path is ever added between ChangeWindow and ReadinessException's own ancestry.
            entity.HasOne(e => e.EmergencyException)
                .WithMany()
                .HasForeignKey(e => e.EmergencyExceptionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.MigrationCampaignId);
            entity.HasIndex(e => e.PlannedStartUtc);
        });

        modelBuilder.Entity<ChangeWindowRunbookStep>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne(e => e.ChangeWindow)
                .WithMany(w => w.RunbookSteps)
                .HasForeignKey(e => e.ChangeWindowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ChangeWindowId);
        });

        modelBuilder.Entity<ChangeWindowApproval>(entity =>
        {
            entity.Property(e => e.RequiredScope).HasMaxLength(200);
            entity.Property(e => e.ApproverName).HasMaxLength(320);
            entity.Property(e => e.ApproverUserId).HasMaxLength(450);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.ChangeWindow)
                .WithMany(w => w.Approvals)
                .HasForeignKey(e => e.ChangeWindowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ChangeWindowId);
        });

        modelBuilder.Entity<CutoverJournalEntry>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.TrafficOrWriteStateNote).HasMaxLength(1000);
            entity.Property(e => e.RecordedBy).HasMaxLength(320);

            entity.HasOne(e => e.ChangeWindow)
                .WithMany(w => w.JournalEntries)
                .HasForeignKey(e => e.ChangeWindowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ChangeWindowId, e.TimestampUtc });
        });

        modelBuilder.Entity<ChangeValidation>(entity =>
        {
            entity.Property(e => e.ObservedTargetBuildText).HasMaxLength(200);
            entity.Property(e => e.ObservedCompatibilityLevel).HasMaxLength(100);
            entity.Property(e => e.ApplicationTestsNotes).HasMaxLength(2000);
            entity.Property(e => e.IntegrityCheckNotes).HasMaxLength(2000);
            entity.Property(e => e.PerformanceComparisonNotes).HasMaxLength(2000);
            entity.Property(e => e.RecordedBy).HasMaxLength(320);

            entity.HasOne(e => e.MigrationCampaign)
                .WithMany()
                .HasForeignKey(e => e.MigrationCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction: same "second cascade path from DatabaseInstance" shape as RehearsalRecord above.
            entity.HasOne(e => e.DatabaseInstance)
                .WithMany()
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.LinkedBackupRecord)
                .WithMany()
                .HasForeignKey(e => e.LinkedBackupRecordId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.LinkedRestoreEvent)
                .WithMany()
                .HasForeignKey(e => e.LinkedRestoreEventId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.DatabaseInstanceId, e.RecordedAtUtc });
            entity.HasIndex(e => e.MigrationCampaignId);
        });

        modelBuilder.Entity<ChangeValidationSignoff>(entity =>
        {
            entity.Property(e => e.ReviewerScope).HasMaxLength(200);
            entity.Property(e => e.ReviewerName).HasMaxLength(320);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.ChangeValidation)
                .WithMany(v => v.Signoffs)
                .HasForeignKey(e => e.ChangeValidationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ChangeValidationId);
        });

        modelBuilder.Entity<ReadinessException>(entity =>
        {
            entity.Property(e => e.TargetType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.WaivedCheckReference).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.CompensatingControls).HasMaxLength(2000);
            entity.Property(e => e.AccountableOwnerName).HasMaxLength(320);
            entity.Property(e => e.PreparedByUserId).HasMaxLength(450);
            entity.Property(e => e.PreparedByUserName).HasMaxLength(320);
            entity.Property(e => e.PermittedApproverRole).HasMaxLength(100);
            entity.Property(e => e.ApprovedByUserId).HasMaxLength(450);
            entity.Property(e => e.ApprovedByUserName).HasMaxLength(320);
            entity.Property(e => e.DecisionNotes).HasMaxLength(1000);
            entity.Property(e => e.EvidenceUrl).HasMaxLength(500);
            entity.Property(e => e.EvidenceNotes).HasMaxLength(2000);
            entity.Property(e => e.RevokedBy).HasMaxLength(320);
            entity.Property(e => e.RevokedReason).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(320);

            entity.HasOne(e => e.AccountableOwnerTeam)
                .WithMany()
                .HasForeignKey(e => e.AccountableOwnerTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            // Restrict, not Cascade: a self-referencing FK must not cascade-delete (SQL Server
            // rejects a self-referencing cascade path outright), and there is no delete feature for
            // exceptions in this app anyway — renewal history is preserved by construction.
            entity.HasOne(e => e.RenewalOfException)
                .WithMany()
                .HasForeignKey(e => e.RenewalOfExceptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.TargetType, e.TargetId });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExpiresAtUtc);
        });
    }
}
