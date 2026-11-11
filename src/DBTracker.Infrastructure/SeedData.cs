using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Infrastructure;

public static class SeedData
{
    public static async Task SeedAsync(DBTrackerDbContext db)
    {
        if (db.DatabaseInstances.Any())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var instances = new List<DatabaseInstance>
        {
            new()
            {
                Name = "ARXMAIL-PROD-01", Engine = DatabaseEngine.PostgreSql, Version = "16.2",
                Application = "ArxMail Gateway", Company = "Architonix Labs", Department = "Platform Engineering",
                ContactName = "Priya Nair", ContactEmail = "priya.nair@architonixlabs.com", ContactPhone = "+1-555-0101",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Production,
                DatabaseIp = "192.168.1.8", ServerIp = "192.168.1.8", ExpectedBackupIntervalHours = 24,
                BackupFileLocation = @"\\192.168.1.8\backups\arxmail-prod",
                LastScanStatus = ScanStatus.Success, LastScanUtc = now.AddMinutes(-40)
            },
            new()
            {
                Name = "ARXMAIL-DR-01", Engine = DatabaseEngine.PostgreSql, Version = "16.2",
                Application = "ArxMail Gateway", Company = "Architonix Labs", Department = "Platform Engineering",
                ContactName = "Priya Nair", ContactEmail = "priya.nair@architonixlabs.com", ContactPhone = "+1-555-0101",
                Role = InstanceRole.Passive, Environment = EnvironmentTier.DisasterRecovery,
                DatabaseIp = "192.168.1.9", ServerIp = "192.168.1.9", ExpectedBackupIntervalHours = 48
            },
            new()
            {
                Name = "AVALOKH-SALES-SQL", Engine = DatabaseEngine.SqlServer, Version = "2022",
                Application = "Avalokh CRM", Company = "Avalokh", Department = "Sales Ops",
                ContactName = "Marcus Chen", ContactEmail = "marcus.chen@avalokh.com", ContactPhone = "+1-555-0110",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Production,
                DatabaseIp = "192.168.1.8", ServerIp = "192.168.1.8", ExpectedBackupIntervalHours = 12
            },
            new()
            {
                Name = "CONTOSO-HR-MYSQL", Engine = DatabaseEngine.MySql, Version = "8.0",
                Application = "Contoso HR", Company = "Contoso", Department = "Human Resources",
                ContactName = "Angela Price", ContactEmail = "angela.price@contoso.com", ContactPhone = "+1-555-0120",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Production,
                DatabaseIp = "192.168.1.8", ServerIp = "192.168.1.8", ExpectedBackupIntervalHours = 24,
                BackupFileLocation = @"\\192.168.1.8\backups\contoso-hr",
                LastScanStatus = ScanStatus.Error, LastScanUtc = now.AddHours(-26),
                LastScanError = "Network path not found — \\\\192.168.1.8\\backups\\contoso-hr unreachable"
            },
            new()
            {
                Name = "EDIQRA-INVENTORY-PG", Engine = DatabaseEngine.PostgreSql, Version = "15.6",
                Application = "Ediqra Inventory", Company = "Ediqra", Department = "Supply Chain",
                ContactName = "Dev Patel", ContactEmail = "dev.patel@ediqra.com", ContactPhone = "+1-555-0130",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Staging,
                DatabaseIp = "192.168.1.8", ServerIp = "192.168.1.8", ExpectedBackupIntervalHours = 24
            },
            new()
            {
                Name = "LPW-CONTENT-MONGO", Engine = DatabaseEngine.MongoDb, Version = "7.0",
                Application = "LifePlusWisdom CMS", Company = "LifePlusWisdom", Department = "Content",
                ContactName = "Dev Patel", ContactEmail = "dev.patel@lifeplus.com", ContactPhone = "+1-555-0131",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Production,
                DatabaseIp = "192.168.1.6", ServerIp = "192.168.1.6", ExpectedBackupIntervalHours = 24
            },
            new()
            {
                Name = "NEXTCLOUD-FILES-MYSQL", Engine = DatabaseEngine.MySql, Version = "8.0",
                Application = "Nextcloud", Company = "Architonix Labs", Department = "IT Operations",
                ContactName = "Priya Nair", ContactEmail = "priya.nair@architonixlabs.com", ContactPhone = "+1-555-0101",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Production,
                DatabaseIp = "192.168.1.6", ServerIp = "192.168.1.6", ExpectedBackupIntervalHours = 24
            },
            new()
            {
                Name = "DIFY-PLUGIN-PG", Engine = DatabaseEngine.PostgreSql, Version = "16.2",
                Application = "Dify", Company = "Architonix Labs", Department = "AI Platform",
                ContactName = "Marcus Chen", ContactEmail = "marcus.chen@architonixlabs.com", ContactPhone = "+1-555-0102",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Development,
                DatabaseIp = "192.168.1.8", ServerIp = "192.168.1.8", ExpectedBackupIntervalHours = 72
            },
            new()
            {
                Name = "GYNATARANGODISHA-ORACLE", Engine = DatabaseEngine.Oracle, Version = "19c",
                Application = "GynatarangOdisha Portal", Company = "GynatarangOdisha", Department = "Finance",
                ContactName = "Angela Price", ContactEmail = "angela.price@gynatarangodisha.org", ContactPhone = "+1-555-0140",
                Role = InstanceRole.Active, Environment = EnvironmentTier.Production,
                DatabaseIp = "192.168.1.6", ServerIp = "192.168.1.6", ExpectedBackupIntervalHours = 24
            },
            new()
            {
                Name = "CONTOSO-SALES-SQL-REPLICA", Engine = DatabaseEngine.SqlServer, Version = "2022",
                Application = "Contoso Sales", Company = "Contoso", Department = "Sales Ops",
                ContactName = "Marcus Chen", ContactEmail = "marcus.chen@contoso.com", ContactPhone = "+1-555-0121",
                Role = InstanceRole.Passive, Environment = EnvironmentTier.DisasterRecovery,
                DatabaseIp = "192.168.1.9", ServerIp = "192.168.1.9", ExpectedBackupIntervalHours = 48
            },
        };

        db.DatabaseInstances.AddRange(instances);
        await db.SaveChangesAsync();

        void AddHistory(DatabaseInstance instance, params BackupRecord[] records)
        {
            foreach (var r in records)
            {
                r.DatabaseInstanceId = instance.Id;
            }
            db.BackupRecords.AddRange(records);
        }

        // On track: recent successful full backup within window.
        AddHistory(instances[0],
            new BackupRecord { TimestampUtc = now.AddHours(-4), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 4820, DurationMinutes = 22 },
            new BackupRecord { TimestampUtc = now.AddDays(-1).AddHours(-4), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 4790, DurationMinutes = 21 });

        // On track, DR passive, slower cadence.
        AddHistory(instances[1],
            new BackupRecord { TimestampUtc = now.AddHours(-10), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 4820, DurationMinutes = 25 });

        // Overdue: last success was 3 days ago, 12h expected cadence.
        AddHistory(instances[2],
            new BackupRecord { TimestampUtc = now.AddDays(-3), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 1230, DurationMinutes = 8 });

        // Failed: most recent attempt failed.
        AddHistory(instances[3],
            new BackupRecord { TimestampUtc = now.AddHours(-2), Type = BackupType.Full, Status = BackupStatus.Failed, Notes = "Disk full on backup target" },
            new BackupRecord { TimestampUtc = now.AddDays(-1).AddHours(-2), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 980, DurationMinutes = 6 });

        // instances[4] (Ediqra) — never backed up.

        // On track, Mongo.
        AddHistory(instances[5],
            new BackupRecord { TimestampUtc = now.AddHours(-6), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 2100, DurationMinutes = 14 });

        // Overdue.
        AddHistory(instances[6],
            new BackupRecord { TimestampUtc = now.AddDays(-2), Type = BackupType.Differential, Status = BackupStatus.Success, SizeMb = 340, DurationMinutes = 3 });

        // On track, dev, long cadence.
        AddHistory(instances[7],
            new BackupRecord { TimestampUtc = now.AddHours(-20), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 610, DurationMinutes = 4 });

        // Missed most recent scheduled run.
        AddHistory(instances[8],
            new BackupRecord { TimestampUtc = now.AddHours(-26), Type = BackupType.Full, Status = BackupStatus.Missed, Notes = "Scheduled job did not run" },
            new BackupRecord { TimestampUtc = now.AddDays(-2), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 2200, DurationMinutes = 18 });

        // On track, passive DR replica.
        AddHistory(instances[9],
            new BackupRecord { TimestampUtc = now.AddHours(-30), Type = BackupType.Full, Status = BackupStatus.Success, SizeMb = 1180, DurationMinutes = 9 });

        await db.SaveChangesAsync();

        // One instance has had a restore proven, so Compliance's "Restore tested" column isn't all No.
        db.RestoreEvents.Add(new RestoreEvent
        {
            DatabaseInstanceId = instances[0].Id,
            TimestampUtc = now.AddDays(-14),
            PerformedBy = "Priya Nair",
            Reason = RestoreReason.DrDrill,
            Outcome = RestoreOutcome.Success,
            Notes = "Quarterly DR drill — restored to staging, verified row counts."
        });

        // Seeded directly so the bell has something to show before the first alert sweep.
        instances[3].LastNotifiedHealth = BackupHealth.Failed;
        instances[3].LastNotifiedUtc = now.AddHours(-2);
        instances[8].LastNotifiedHealth = BackupHealth.Failed;
        instances[8].LastNotifiedUtc = now.AddHours(-26);
        instances[2].LastNotifiedHealth = BackupHealth.Overdue;
        instances[2].LastNotifiedUtc = now.AddDays(-1);

        db.Notifications.AddRange(
            new Notification
            {
                DatabaseInstanceId = instances[3].Id,
                InstanceName = instances[3].Name,
                Company = instances[3].Company,
                Message = $"{instances[3].Name} is now failing backups.",
                Severity = NotificationSeverity.Critical,
                CreatedAtUtc = now.AddHours(-2)
            },
            new Notification
            {
                DatabaseInstanceId = instances[8].Id,
                InstanceName = instances[8].Name,
                Company = instances[8].Company,
                Message = $"{instances[8].Name} is now failing backups.",
                Severity = NotificationSeverity.Critical,
                CreatedAtUtc = now.AddHours(-26)
            },
            new Notification
            {
                DatabaseInstanceId = instances[2].Id,
                InstanceName = instances[2].Name,
                Company = instances[2].Company,
                Message = $"{instances[2].Name} is now overdue for backup.",
                Severity = NotificationSeverity.Warning,
                CreatedAtUtc = now.AddDays(-1),
                IsRead = true
            });

        // Seeded directly because the alert sweep is disabled in Testing (Alerts:IntervalMinutes: 0).
        db.Incidents.Add(new Incident
        {
            DatabaseInstanceId = instances[3].Id,
            TriggerHealth = BackupHealth.Failed,
            OpenedAtUtc = now.AddHours(-2)
        });

        await db.SaveChangesAsync();

        // Lifecycle seed data: ARXMAIL-PROD-01 has an attested identity with a support deadline inside the 30-day notice window;
        // CONTOSO-HR-MYSQL has a more precise manual version with a full patch-baseline and advisory story.
        db.VersionIdentities.AddRange(
            new VersionIdentity
            {
                DatabaseInstanceId = instances[0].Id,
                RawVersionText = "16.2",
                ResolutionStatus = VersionResolutionStatus.Resolved,
                Family = "PostgreSQL",
                Major = 16,
                Minor = 2,
                ObservationSource = VersionObservationSource.Attested,
                IsAttested = true,
                AttestedBy = "Priya Nair",
                AttestedAtUtc = now.AddDays(-5)
            },
            new VersionIdentity
            {
                DatabaseInstanceId = instances[3].Id,
                RawVersionText = "8.0.36",
                ResolutionStatus = VersionResolutionStatus.Resolved,
                Family = "MySQL",
                Major = 8,
                Minor = 0,
                Build = "36",
                ObservationSource = VersionObservationSource.ManualEntry,
                UpdatedBy = "Angela Price"
            });
        await db.SaveChangesAsync();

        var pgInstalled = new ReleaseCatalogEntry
        {
            Engine = DatabaseEngine.PostgreSql, Family = "PostgreSQL", ReleaseLabel = "PostgreSQL 16.2",
            Major = 16, Minor = 2, Channel = ReleaseChannel.GA,
            MainstreamSupportEndUtc = now.AddDays(20), ReviewedAtUtc = now.AddDays(-5), ReviewedBy = "Priya Nair",
            SourceUrl = "https://www.postgresql.org/support/versioning/"
        };
        var mysqlInstalled = new ReleaseCatalogEntry
        {
            Engine = DatabaseEngine.MySql, Family = "MySQL", ReleaseLabel = "MySQL 8.0.36",
            Major = 8, Minor = 0, Build = "36", Channel = ReleaseChannel.GA,
            ReviewedAtUtc = now.AddDays(-30), ReviewedBy = "Angela Price",
            SourceUrl = "https://dev.mysql.com/doc/refman/8.0/en/upgrading.html"
        };
        var mysqlNewerPatch = new ReleaseCatalogEntry
        {
            Engine = DatabaseEngine.MySql, Family = "MySQL", ReleaseLabel = "MySQL 8.0.43",
            Major = 8, Minor = 0, Build = "43", Channel = ReleaseChannel.GA,
            ReviewedAtUtc = now.AddDays(-10), ReviewedBy = "Angela Price",
            SourceUrl = "https://dev.mysql.com/doc/refman/8.0/en/upgrading.html"
        };
        var mysqlNewerGa = new ReleaseCatalogEntry
        {
            Engine = DatabaseEngine.MySql, Family = "MySQL", ReleaseLabel = "MySQL 8.4 LTS",
            Major = 8, Minor = 4, Build = "0", Channel = ReleaseChannel.GA,
            ReviewedAtUtc = now.AddDays(-10), ReviewedBy = "Angela Price",
            SourceUrl = "https://dev.mysql.com/doc/refman/8.4/en/upgrade-paths.html"
        };
        db.ReleaseCatalogEntries.AddRange(pgInstalled, mysqlInstalled, mysqlNewerPatch, mysqlNewerGa);
        await db.SaveChangesAsync();

        db.ApprovedPatchBaselines.Add(new ApprovedPatchBaseline
        {
            Engine = DatabaseEngine.MySql, Family = "MySQL",
            TargetReleaseCatalogEntryId = mysqlNewerPatch.Id,
            ReviewedBy = "Angela Price", ReviewedAtUtc = now.AddDays(-10),
            Notes = "Approved same-branch patch target — not a major-version migration."
        });

        db.AdvisoryRecords.Add(new AdvisoryRecord
        {
            Engine = DatabaseEngine.MySql, Family = "MySQL",
            VendorAdvisoryId = "CVE-2025-0001", Title = "Example privilege-escalation advisory (seed/demo data)",
            Severity = AdvisorySeverity.High,
            AffectedMinBuild = "8.0.0", AffectedMaxBuild = "8.0.40", FixedBuild = "8.0.41",
            ProvenanceUrl = "https://www.oracle.com/security-alerts/",
            PublishedAtUtc = now.AddDays(-20), ImportedBy = "Angela Price"
        });

        await db.SaveChangesAsync();
    }
}
