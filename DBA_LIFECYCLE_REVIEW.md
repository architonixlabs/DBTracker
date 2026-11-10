# DBA lifecycle and operational readiness review

Date: 2026-11-10. This extends the earlier usability review with database administration,
release/support lifecycle, migration, and related operational needs. Proposed work is appended
to [BACKLOG.md](BACKLOG.md#dba-lifecycle-roadmap---2026-11-10); earlier entries are unchanged.

## Assessment and limits

DBTracker is currently an inventory and backup-evidence tracker. A DBA also needs to answer:
What exactly is installed? Is it still receiving applicable fixes? Is a newer release relevant?
What prevents an upgrade? Can we recover if it fails? Who accepts the outage and residual risk?
The current model cannot answer those questions reliably.

This assessment uses repository inspection and official vendor guidance, not interviews or a live
fleet assessment. No database connections, upgrades, scans, migrations or external notifications
were executed. No installed instance is declared unsupported or vulnerable by this review.
Vendor pages can change; exact dates, builds and upgrade paths must be checked again when used.
The roadmap is a proposal, not an authorization to execute upgrades or expand collection access.

## Current implementation evidence

| Repository evidence | Existing behavior | DBA consequence / proposed coverage |
|---|---|---|
| `src/DBTracker.Core/Entities/DatabaseInstance.cs` | Engine enum and one free-text Version; no edition/build/distribution/provider or support entitlement | Cannot reliably map installed software to lifecycle or patch rules: D01-D05 |
| `src/DBTracker.Core/Enums/DatabaseEngine.cs` | SQL Server, MySQL, PostgreSQL, MongoDB, Oracle, Other | Need engine-specific rules and explicit unknown/custom products rather than universal version comparisons: D01-D02 |
| `src/DBTracker.Core/Services/InstanceCsvImporter.cs` | Version is imported as text | Preserve original text, provenance and unresolved mappings when adding structured identity: D01/D06 |
| `src/DBTracker.Infrastructure/Queries/InstanceOverviewService.cs` | Health derives from backup records | Support risk must be a separate dimension; green backup health does not imply supported software: D03/D15 |
| `src/DBTracker.Core/Services/BackupHealthCalculator.cs` | One expected interval for all backup types | A recent log entry cannot establish a valid full/differential/log recovery chain or achieved RPO: D16 |
| `src/DBTracker.Infrastructure/Scanning/BackupFileScanner.cs` | Filesystem evidence, no database metadata collection | Cannot discover builds, dependencies, provider restrictions, storage health or replication state: D06/D07/D17-D19 |
| `src/DBTracker.Infrastructure/Notifications/AlertCheckHostedService.cs` | Overdue/Failed backup state changes only | No support-deadline, patch, advisory or migration-overdue routing: D03-D05/D15 |
| `src/DBTracker.Core/Entities/RestoreEvent.cs` | Timestamp, tester, reason, outcome, notes | Cannot link a migration rehearsal to source/target builds, measured recovery or rollback evidence: D11/D16 |
| `src/DBTracker.Web/Components/Pages/Instances/InstanceDetail.razor` | Immediate maintenance windows of fixed durations and generic notes/runbook | No future migration schedule, readiness gates, change approvals or cutover journal: D09-D14 |
| `src/DBTracker.Web/Components/Pages/Reports.razor` | Backup activity, restore, ownership, scan, compliance, management reports | No support runway, patch coverage or upgrade campaign reporting: D15 |
| `src/DBTracker.Core/Services/InstanceChangeLogBuilder.cs` | Captures a Version text edit | Editing the version is not evidence of successful migration: D13; use proposed U19 for broader audit coverage |

## Personas: challenges and scope agreements

These are simulated analytical perspectives. Agreement records a useful design resolution,
not interviews, stakeholder sign-off or consensus from independent people.

| Persona | Questions they bring | Challenge / agreed requirement |
|---|---|---|
| Priya, production DBA | Which patch/major release is appropriate? What are the blockers and rollback limits? | Separate patching from major upgrades; record actual build and compatibility state; never use a universal upgrade path |
| Dev, application owner | Will drivers, queries, scheduled integrations and licensed applications still work? | Application certification and representative tests are explicit gates; newest GA is not automatically an approved target |
| Riya, security engineer | Are applicable fixes missing? Does an exception still have an accountable owner? | Vendor advisory applicability needs exact product/build/context; EOL and vulnerability are separate facts; exemptions expire |
| Noor, SRE/on-call | What breaks during cutover? How do we recover writes after switching? | Rehearsal, replication/backup dependencies, stop conditions and point of no return are visible; old binaries alone are not a rollback plan |
| Sam, platform/cloud administrator | Does the provider offer this release, allow this path, or force a deadline? | Track distribution, hosting/service/region and provider notices independently from upstream lifecycle |
| Marcus, service owner/IT manager | What is the business exposure, budget and achievable completion date? | Prioritize critical services and lead time; make retire/replace/extended-support alternatives visible |
| Angela, auditor/change manager | Who approved the risk and what proved the change worked? | Versioned source facts, approvals, evidence and post-change sign-off survive edits and source changes |
| Leena, procurement/asset manager | Is paid support active? Does target edition change cost/entitlement? | Separate technical lifecycle facts, internal policy and contract evidence; missing contract data remains unknown |

## Vendor differences that shape the design

| Engine / context | Verified guidance | Product implication (inference) |
|---|---|---|
| PostgreSQL | Community supports major releases for five years; major and minor upgrade procedures differ. [Versioning policy](https://www.postgresql.org/support/versioning/) | Track support runway per major and patch freshness within a branch; do not use a generic semantic-version rule for historical versions |
| SQL Server | Mainstream, extended and ESU phases differ; servicing has CU/GDR branches. [Support options](https://learn.microsoft.com/en-us/sql/sql-server/end-of-support/sql-server-end-of-support-overview), [servicing models](https://learn.microsoft.com/en-us/troubleshoot/sql/releases/servicing-models-sql-server) | Separate support phase, applicable patch branch and actual ESU coverage; mainstream end alone does not mean all support has ended |
| MySQL | LTS/Innovation support differs; sustaining support supplies pre-existing fixes. Upgrade paths specify intermediate steps and methods. [Support policy](https://www.mysql.com/support/index.html), [8.4 upgrade paths](https://dev.mysql.com/doc/refman/8.4/en/upgrade-paths.html) | Branch-aware targets and a reviewed path are needed; a numeric jump to a newer release can be invalid |
| MongoDB | Lifecycle schedules distinguish Atlas offerings and Enterprise Advanced. Upgrade guidance separates binaries, drivers and feature compatibility. [Lifecycle schedules](https://www.mongodb.com/legal/support-policy/lifecycles), [8.0 upgrade guidance](https://www.mongodb.com/docs/manual/release-notes/8.0-upgrade-standalone/) | Match deployment context and compatibility state separately; retain source-specific rules and flag inconsistent instructions for review |
| Oracle Database | Premier, Extended and Sustaining offer different maintenance coverage. [Lifetime Support](https://www.oracle.com/uk/support/lifetime-support/) | Never collapse access to old fixes/support into availability of new security fixes; store contract and release-specific evidence rather than assuming entitlement |
| Managed PostgreSQL | Provider version policy adds service-specific consequences to upstream retirement. [Azure PostgreSQL version policy](https://learn.microsoft.com/azure/postgresql/configure-maintain/concepts-version-policy) | Upstream and provider milestones need separate facts and applicability, even for the same engine version |

For migration planning, Microsoft describes assessment, application validation, edition/features,
and the inability to restore newer-version SQL Server backups onto older engines. This supports
explicit compatibility and rollback gates rather than a generic downgrade button.
[SQL Server upgrade guide](https://learn.microsoft.com/en-us/sql/sql-server/migrate/guides/sql-server-to-sql-server-upgrade-guide).

Source quality observation: retrieved vendor pages may contain mixed version references. The
MongoDB 8.0 upgrade page retrieved during this review contains inconsistent version labels.
It is useful evidence that binaries and feature compatibility are distinct, but should not be
turned into executable instructions without review. DBTracker should preserve the source and
mark ambiguous/conflicting rules as needing review, rather than silently selecting a value.

## How a DBA should make the decision

Keep these four questions separate in the UI and data model:

1. **Available:** has a relevant release been published for this product and deployment?
2. **Applicable:** does it fix a relevant issue or offer a supported path from this build?
3. **Approved:** is this target certified internally and accepted by the application/platform owners?
4. **Required by:** is there a support/security/provider/internal-policy deadline and an agreed action?

A newer GA release is information, not an automatic migration mandate. A mandatory action can
instead be patching within the current branch, buying applicable extended coverage, moving to a
provider-supported target, replacing the application, or retiring the database. Those alternatives
must keep separate owners, approval and evidence. Cross-engine migration needs its own assessment;
it is not an engine-version upgrade with a different label.

The DBA workspace should show installed identity and observation age, support phase and dates,
approved target and path, relevant advisories, blockers, owner, next milestone and evidence links.
Do not hide unknown identity/catalogue freshness behind a green state. Avoid one opaque score:
show business criticality, exposure, time to deadline and lack of coverage as explainable factors.

## Proposed information model

| Record | Minimum useful information |
|---|---|
| Deployment / database / node | Stable identity, service/application links, hosting, environment, topology and nodes; distinguish a database from its hosting engine and cluster |
| Observed software | Raw version, normalized family/branch/build, edition/distribution, patch train, compatibility state, OS/provider context, source and observed-at time |
| Release/support fact | Product/context, release and milestone type, date precision/timezone, applicability, source URL/document reference, retrieved/reviewed times and revision |
| Support entitlement | Coverage type, affected assets, evidence reference, owner, effective/expiry dates; no contract secrets in public reports |
| Approved baseline | Target builds/branches, internal policy version, review/expiry, permitted exceptions and supporting compatibility decisions |
| Advisory assessment | Vendor ID/source, affected ranges/configuration, applicability evidence, potential/confirmed/unknown status, remediation and owner |
| Change campaign | Patch/upgrade/relocation/cross-engine/retirement type, source/target, method, prerequisites, dependencies, schedule, outage and accountable owners |
| Evidence and approval | Rehearsal/baseline/test records, decision maker, scope, result, timestamp, policy/version; superseding record instead of silent overwrite |

Proposed entities are design guidance, not a prescribed migration schema. Preserve the existing
Version text and IDs; add structured mappings with unknown states. Use U19 audit conventions,
U21 ownership and U10 snapshots instead of parallel implementations. Vendor data should be
reviewable and importable offline; a failed refresh must retain last-known facts with a staleness
warning. Source-policy updates need revision records and affected-asset reevaluation.

## Full workflow and completion gates

1. Inventory/attest actual deployment and version; resolve unmapped or stale observations.
2. Evaluate release availability, support/contract phase, security applicability and internal policy.
3. Select and justify patch/upgrade/migrate/retire/exception; record target and accountable owner.
4. Assess application, driver, extension, OS, edition, topology and tooling compatibility; obtain
   version-specific assessment evidence. Unknown results are incomplete, not passing.
5. Rehearse the method, backup recovery and rollback on an appropriate test system; compare
   application correctness/performance against a documented baseline and acceptable outage/RPO/RTO.
6. Approve a scheduled change with communication, freeze conflicts, stop conditions and recovery
   ownership. An urgent exception is recorded explicitly rather than pretending every gate passed.
7. Track cutover steps and write/traffic state; capture failures and the rollback decision. The
   tracker records externally performed actions; it does not run commands against the database.
8. Observe target builds, verify application/DBA checks, backups and replicas, and obtain sign-off.
   Marking a version field or completing an installation step does not complete the migration.
9. Preserve rollback assets for their agreed window; retire the source only after final validation,
   evidence-retention and dependencies are resolved. Recheck risk after coverage/exception expiry.

## Delivery recommendation

- **First, lifecycle visibility:** D01-D04 plus basic D15 views. Curate facts manually before
  building live feeds. Existing inventory can be attested without credentials or new collectors.
- **Next, controlled decisions:** D07-D14. Use imported/referenced assessment evidence before
  implementing engine-specific parsers; reviewers retain responsibility for applicability.
- **Then, stronger operational context:** D05/D06 and D16-D22. Prioritize security triage and
  recovery assurance for critical services; integrate existing monitoring/ticketing results rather
  than building a complete performance monitor, secrets manager or procurement system.

P1 means a proposed near-term foundation/high-value item, not a claim that the fleet has an
emergency. Live collection (D06) is optional; manual evidence and import paths must work first.
Risk visibility can ship before campaign automation, but must expose incomplete readiness.

## Acceptance exercises and meaningful tests

- Import versions with suffixes, missing builds, ambiguous distributions and multiple cluster
  members. Original strings remain available; unresolved records are Unknown, never inferred safe.
- Evaluate synthetic support milestones yesterday/today/tomorrow and an unknown/TBD date. Preserve
  vendor precision/timezone; paid coverage requires evidence; expiry recomputes state correctly.
- Publish a newer release without approved compatibility: Available changes, but no automatic
  Required upgrade or Approved target appears. A preview release cannot become the default target.
- Fail a catalogue refresh or revise a vendor date. Last-known facts remain visible with age;
  changes have a review history and notifications are deduplicated by milestone/revision policy.
- Give two deployments the same upstream version but different distribution/provider/coverage.
  Compute their applicable status independently and retain the reasons.
- Submit a proposed direct version jump with an unsupported/missing path. Show Blocked/Unknown
  and the relevant evidence; do not fabricate intermediate steps.
- Upgrade cluster binaries but leave a member/compatibility state behind. The campaign stays
  incomplete until the documented target topology and sign-off criteria are met.
- Fail a rehearsal, restore-chain check or application test. Record a blocking result; a specific
  authorized exception is visible and expires without erasing the failed evidence.
- Simulate rollback after target writes. Require the documented data reconciliation/loss decision;
  restoring an older backup is not presented as preserving newer writes automatically.
- Close a migration without a new successful backup or application acceptance. Reject normal
  completion or record a permitted exception with scope, approver and expiry.
- Validate role boundaries: DBA proposes technical work; assigned app/security/change reviewers
  approve their scope; viewers cannot approve by UI or direct action. Record actor and scope.
- Test recovery with a recent log backup but a missing base/chain member. Display unknown or
  broken recoverability despite a recent successful record; no unsupported RPO claim.
- Retire a source with unresolved clients or retained recovery dependencies. Block retirement or
  require a specific reviewed exception; preserve history and affected report denominators.

Use synthetic fixtures for logic and source-adapter tests; a unit test must not depend on today's
live vendor website. Run deployment-specific compatibility/performance/recovery exercises before
actual production changes. This documentation change does not establish those results.

## Scope boundaries and links to earlier work

D01-D22 are additional DBA lifecycle/operations work. U01/U04/U11/U12 still govern backup evidence,
health explanation and restore entry; D16 adds recovery objectives and chain-level interpretation.
U08 is the generic incident workflow; D03/D05 add lifecycle/advisory-specific facts and rules.
U19/U21 supply audit and organizational foundations; D07 adds technical dependency relationships.
Existing maintenance suppresses alerts temporarily; D12 adds scheduled migration coordination and
gates without rewriting maintenance as proof that a change is safe.

Automatic database upgrades, destructive migration execution, automatic failover, backup deletion,
license/legal certification, vulnerability exploitation, and credential collection are outside this
proposal. Any later execution capability needs a separate explicit design and authorization.
No infrastructure standard, mail provider, runtime database, or current application behavior was
changed by this review.
