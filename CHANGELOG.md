# Changelog

All notable changes to DBTracker are recorded here. Versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- **Archive and retirement.** A plain archive now asks whether the record is a duplicate or being
  decommissioned. It is refused while a retirement checklist is open, so a decommission can't skip
  the checklist. Opening the retirement page no longer starts a checklist, and a checklist can be
  cancelled with a reason. The rollback gate no longer passes when a change was validated without a
  rollback deadline.
- **Notifications.** A new user's first alerts no longer arrive already marked read.
- **Change windows.** A window can only be completed or rolled back after it was in progress, and
  needs at least one required approval.
- **Bulk edit.** Bulk edit is all or nothing:
  - It checks the values and saves nothing if any instance would end up invalid.
  - It skips instances changed since the preview.
  - It rechecks the user's role, and applies exactly the previewed values.
  - Audit entries share a batch id and are saved with the changes.
- **Accounts.**
  - Invitations email a link to set a password and no longer contain the temporary password.
  - A user with a temporary password is returned to the change-password page from every page.
  - Any role change ends the old role's sessions and pending device links.
  - Creating a user is always audited.
- **Cost figures.** Cost estimate amounts no longer appear in the audit trail or its export, or
  through its search, for roles that can't see them. DBAs can record support contracts, which carry
  no amounts.
- **Backup scanning.** A backup file rewritten under the same name and size is recorded as a new
  backup. Log backups (`.trn`, or a `log` part in the name) and differentials are recorded as such
  instead of as full backups, and the scan preview shows the type.
- **Documentation.**
  - The 1.0.0 notes below say the tray restarts the site; it starts and stops the site and restarts
    the app pool.
  - `installer\build.ps1` now writes `SHA256SUMS.txt`, which the setup guide refers to.
  - DEPLOYMENT.md explains that tray access tokens survive uninstall and how to revoke them.

## [1.0.0] - 2026-11-11

First public release (pre-release). DBTracker keeps an inventory of your database instances and shows,
for each one, whether its backups are actually happening and can be restored.

### Install and run

- **Setup.exe** checks what the server is missing and, after one confirmation, turns on IIS with the
  WebSocket Protocol, installs the .NET 10 Hosting Bundle and, when no SQL Server is found, can install
  SQL Server Express. Every download comes from Microsoft and is checked against Microsoft's published
  hash or signature. It then installs DBTracker into IIS and opens the setup wizard.
- **DBTracker.msi** installs the app, the IIS site and app pool, the App_Data permission and,
  optionally, a SQL login for the app pool. Shows in Settings > Apps; uninstall keeps your data unless
  you ask otherwise (`DBTracker-Setup.exe /uninstall RemoveData=1`).
- **Setup wizard** at `/install` on first start: one-time setup key, SQL Server connection (create the
  database or reuse an existing one), table creation with progress, optional sample data, the first
  Admin account, optional email (ArxMail or SMTP) and a backup-share access check. Secrets are stored
  encrypted.
- **DBTracker Tray** next to the clock: status light from `/healthz`, open DBTracker, start / stop /
  restart the app pool and site, folders and logs, recent errors, and counts of failed, overdue and
  never-backed-up instances with alerts for new ones.
- `web.config`, a folder publish profile and [DEPLOYMENT.md](DEPLOYMENT.md) for manual IIS installs.

### Inventory and backup health

- Inventory of SQL Server, MySQL, MongoDB, PostgreSQL and other instances with owner, environment,
  active/passive role, addresses, retention, tags, runbook link, team ownership and region / cluster /
  replica-set facets. CSV import with preview and repeatable matching; reviewed bulk edits.
- Health per instance (On track, Overdue, Failed, Never backed up) with a plain explanation: last
  success, latest attempt, interval, next due time, maintenance suppression.
- Backup evidence from a shared drive: scheduled and on-demand scans, file-name rules with preview,
  evidence levels (discovered, reported, restore-tested), stale-scanner detection.
- Restore logging and restore-test recency; backdated entries with append-only corrections.
- Maintenance windows, TCP connectivity check with configurable endpoints, archive with preview and
  unarchive, change history for every edit.

### Reports and notifications

- Dashboard and instance list with saved, shareable views and server-side paging.
- Reports center: backup activity, restore testing, ownership roll-up, scan activity, trends, and
  frozen compliance snapshots with custom date ranges; CSV export everywhere.
- Scheduled management report by email with preview, explicit scope and honest delivery outcomes.
- Personal notification inbox with subscriptions; alerts by email and generic webhook; incident
  acknowledgement, assignment and escalation that never changes the health shown.

### DBA lifecycle

- Version identity, a reviewed release and support catalogue, support runway with deadline notices,
  approved patch baselines and advisory applicability.
- Dependency mapping, source-to-target path assessment, upgrade-readiness checklists and migration
  campaigns.
- Rehearsal evidence, change windows with a cutover journal, post-change validation and expiring,
  approved exceptions.
- Recovery objectives and backup-chain coverage, backup-copy availability, integrity / HA / maintenance
  evidence, capacity planning, certificate and recovery-key reminders.
- Support contracts and edition-cost estimates; a decommission checklist that archives only after its
  gates pass.

### Accounts and security

- Roles: Admin, DBA, IT Manager, Auditor, Viewer, enforced on pages and actions.
- Account lifecycle: change roles, deactivate and reactivate, password reset, forced change of a
  temporary password, sign-in from a phone by QR code.
- Personal access tokens at `/account/tokens` (hashed, expiring, revocable, audited) for the tray app.
- Audit trail across operational changes.

### Known issues

- The installers and the tray app are **not code-signed**. Windows SmartScreen shows "Windows
  protected your PC" (choose More info, then Run anyway), and Smart App Control on Windows 11 can delay
  or block them.
- Setup.exe was tested in its quiet and passive modes; clicking through its Options page by hand has
  not been tested yet.
- Changing your own password signs you out; sign in again with the new password.
- Windows Home and Windows Sandbox can't run DBTracker: they don't include IIS.
- The optional sample data creates five demo accounts with a published password. Leave it unticked on
  a real server.

[1.0.0]: https://github.com/architonixlabs/DBTracker/releases/tag/v1.0.0
