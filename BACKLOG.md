# DBTracker — Product Backlog

## Purpose
Track every database instance across the org (SQL Server, MySQL, MongoDB, PostgreSQL, etc.) and
verify that backups are actually happening — who owns each DB, where it lives, and whether its
last backup succeeded, is overdue, or never ran.

## Personas

### 1. Priya — DBA (primary user)
Owns backup health day to day. Needs to see, at a glance, which instances are overdue or failed,
drill into backup history, and update backup status after running a job manually.
- Wants a dashboard sorted by "most at risk" first.
- Wants to filter by engine, environment, active/passive, owner.
- Wants to log a backup result in under 10 seconds.

### 2. Marcus — IT Manager
Owns headcount/vendor relationships per application. Cares about ownership and contact info being
correct so he knows who to call when something breaks, and wants a roll-up by department/company.
- Wants to see "who owns this" without digging.
- Wants export/reporting for leadership.

### 3. Angela — Auditor / Compliance
Needs evidence backups ran, on a cadence, for every production database, for audit season.
- Wants a point-in-time report: "as of today, which prod DBs have a backup in the last 24/48h?"
- Needs immutable-ish history (no silent deletion of past backup records).
- Needs to filter by active/passive and environment (don't flag DR passive replicas the same way).

### 4. Dev — End User / App Owner
Just wants to confirm their app's DB is backed up and update contact info when staff changes.
- Wants a simple read-only view scoped to "my applications."
- Low tolerance for clutter — wants the grid, not a wall of fields.

## Backlog (prioritized)

### Epic 1 — Core inventory (MVP)
- [x] Data model: DatabaseInstance (name, engine, version, app, company, department, contact
      name/email/phone, active/passive, environment, DB IP, server IP, notes, created/updated)
- [x] CRUD UI for database instances (list, create, edit, delete/archive)
- [x] Filterable, sortable grid: engine, environment, active/passive, company, department, owner,
      free-text search
- [x] Seed data covering SQL Server, MySQL, MongoDB, PostgreSQL, mixed active/passive

### Epic 2 — Backup tracking
- [x] Data model: BackupRecord (instance FK, timestamp, type [Full/Diff/Log], status
      [Success/Failed/Missed], size MB, duration, notes)
- [x] Backup history view per instance (timeline, most recent first)
- [x] "Log a backup result" quick-entry form
- [x] Computed backup health per instance: On Track / Overdue / Failed / Never Backed Up, based on
      expected cadence vs. last successful backup
- [x] Dashboard: counts by health status, list of at-risk instances first

### Epic 3 — Filtering & reporting
- [x] Combined filter bar (engine, status/health, active/passive, environment, company/department,
      search) shared between dashboard and inventory views
- [x] Compliance view: as-of snapshot of last-backup age per instance, exportable to CSV

### Epic 4 — Quality
- [x] Unit tests for backup-health computation and filtering logic
- [x] Playwright E2E: create instance → log backup → verify health badge updates → filter → verify
      results

### Epic 5 — RBAC (users & roles)
- [x] ASP.NET Core Identity, roles: Admin, DBA, ITManager, Auditor, Viewer
- [x] Login/logout; seed one user per persona (Priya=DBA, Marcus=ITManager, Angela=Auditor,
      Dev=Viewer) plus an Admin account — dev/testing-only credentials, see IdentitySeedData
- [x] Route/action guards: DBA+Admin can create/edit instances and log backups; Auditor/ITManager/
      Viewer are read-only; enforced at both UI (AuthorizeView) and route level ([Authorize])
- [x] Nav reflects current user + role; hide actions the role can't perform rather than just erroring
- [x] Admin "Users" management page — list users + roles, create a new user with a role (delivered
      under Epic 8, see below)
- [ ] Deactivate an existing user / change an existing user's role from the Users page — currently
      create-only; changing a role today means editing seed data or the database directly

### Epic 6 — Backup evidence from shared drive
*Serves Priya (DBA, who shouldn't have to hand-log every backup) and Angela (Auditor, who needs
proof it's real file evidence, not a self-reported checkbox).*
- [x] Per-instance configurable backup file location (UNC path / folder pattern)
- [x] Scanner that reads file name/timestamp/size at that location and auto-creates BackupRecord
      entries (`Source`: Discovered vs Manual), matched by file name so re-scans never duplicate
- [x] Scheduled recurring scan (`BackupScan:IntervalMinutes`, default 15; 0 disables it) plus a
      manual "Scan now" button on the instance page for an on-demand check
- [x] Last-scanned time and scan errors (path unreachable etc.) surfaced on the instance detail page
- [ ] Surface staleness on the dashboard too (currently detail-page only)

### Epic 6b — Restore tracking
*Serves Angela (Auditor — "has this backup ever actually been proven restorable?") and Priya (DBA —
needs a record of DR drills and emergency restores, not just backups going out).*
- [x] Log a restore event per instance: when, who performed it, why (DR drill / incident / data
      request / test), and outcome (success/failure)
- [x] Restore history visible alongside backup history on the instance detail page
- [x] Compliance view flags instances with zero successful restore history ("Restore tested" column,
      also in the CSV export) — untested backups are a known audit red flag

### Epic 7 — Richer environment & instance detail
- [x] Retention policy field (days backups should be retained — informational)
- [x] Tags field and a runbook/monitoring link field
- [ ] Custom/extensible environment tags beyond the fixed tier (region, cluster, replica set)
- [ ] Audit trail of who changed what on an instance record

### Epic 8 — Notifications
- [x] Configurable email sender (SMTP — Gmail, Office365, any relay — or the ArxMail Gateway's HTTP
      API; `Email:Provider` picks one, defaults to None which only logs) for alerts when an
      instance goes Overdue or Failed
- [x] In-app notification center: bell with unread badge in the sidebar, dropdown list, mark-all-read
- [x] Alerts fire on a *state change* into Overdue/Failed (not every sweep) via a background check
      (`Alerts:IntervalMinutes`, default 10) — re-alerts if it recovers and relapses later
- [ ] Per-user notification preferences (which instances/companies they care about) — currently
      broadcast to everyone, emailed to the instance's own contact
- [x] (bonus, fixes a dangling nav link from Epic 5) Admin "Users" page: list users + roles, create
      a new user with a role

### Epic 9 — Mobile QR login
- [x] Desktop shows a QR code tied to a one-time login token (device-linking flow, like WhatsApp Web)
- [x] Phone scan (already signed in on phone — this never bypasses password login, an already-
      trusted device just vouches for a new one) approves/denies; desktop polls and signs itself in
      automatically within ~2s of approval
- [x] Token expiry (5 min) + single use (marked Redeemed immediately, can't be replayed)

### Epic 10 — Management reporting
*Serves Marcus (IT Manager — needs a leadership-ready roll-up, not raw grid access) and Angela
(Auditor — needs a point-in-time report she can file as evidence).*
- [x] Scheduled report generation (`Reports:IntervalDays`, default weekly) summarizing backup
      health, overdue/failed counts, and restore-test coverage, reusing the Compliance view's data
- [x] Sent via the same configurable email sender as alerts (Epic 8 — Gmail, ArxMail, etc.) to a
      configured distribution list (`Reports:DistributionList`)
- [x] On-demand "Send report" button on Compliance (Admin/IT Manager/Auditor — Marcus and Angela's
      roles, not DBA-gated) for ad-hoc requests before a review

### Epic 11 — Reports center
*User asked for "a report section with different types of reports." Validated per-persona before
building: Priya (DBA) wanted activity over time, not just current state, to spot a flaky job or
duration creep — Backup Activity did that. Marcus (IT Manager) wanted risk rolled up by who owns
it, not a flat grid — Ownership Roll-up did that; Compliance Snapshot and Management Summary
already covered his leadership-report need from Epic 10. Angela (Auditor) wanted Restore Testing
broken out as its own report (an untested backup is a known audit finding, and it was previously
just one column buried in Compliance) plus evidence the shared-drive scanner itself is healthy —
Scan Activity did that. Dev (Viewer) needed nothing new — reports are view-only to every
authenticated role already, same as the rest of the app, so no separate "my apps" cut was added.*
- [x] New `/reports` page, linked from the sidebar nav, with a report-type switcher
- [x] Backup Activity report — runs in a selectable window (7/30/90 days), success/failed/missed
      counts, total size, and the full run list; CSV export
- [x] Restore Testing report — every instance's tested/untested status, last successful restore,
      and total attempts, untested instances sorted first; CSV export
- [x] Ownership Roll-up report — instance counts and health breakdown grouped by company/department
      with primary contacts; CSV export
- [x] Scan Activity report — shared-drive scan status (last run, errors) for every instance with a
      backup file location configured, errored scans sorted first; CSV export
- [x] Compliance Snapshot and Management Summary surfaced in the same hub (link out / inline
      preview + the existing "Send now") rather than duplicating Epic 3/10's logic
- [x] Report math (date filtering, aggregation, grouping) factored into pure, unit-tested builders
      in `DBTracker.Core.Services`, same pattern as `BackupHealthCalculator`/`ComplianceReportBuilder`

## Design
- [x] ~~Visual redesign: indigo/violet gradient accent system~~ — superseded, see below
- [x] ~~Editorial redesign: warm paper background, Fraunces serif + Inter, terracotta/teal accents~~
      — applied from the `/design` artifact, then superseded (see Progress log, 2026-11-10): user
      flagged it as itself a recognizable "AI redesign" look
- [x] Current: dark ops-console theme — IBM Plex Mono for data/nav/headings, Inter for body prose,
      amber/green/red status LEDs with a glow instead of pill badges, hairline dividers instead of
      boxed cards, grid-paper texture background, `// eyebrow` comment-style section labels, a
      terminal-window login card (title bar, `>` prompt fields, blinking cursor)
- [x] Motion: page fade-in, toast slide-in, skeleton shimmer loading states, blinking terminal
      cursor on the brand wordmark, pulsing glow on Failed indicators (draws the eye to what needs
      attention first)
- [x] Toast notifications on create/update/log-backup/archive actions
- [ ] Further polish: empty/zero-state illustrations, a light-theme toggle (dark is currently the
      only theme, by design — not a gap, but a toggle isn't built), responsive mobile pass beyond
      the basic sidebar collapse

## Progress log
- 2026-11-09 — Repo initialized; solution scaffolded (Web/Core/Infrastructure/UnitTests/E2E); domain
  model (DatabaseInstance, BackupRecord) + BackupHealthCalculator written; Bootstrap-based sidebar
  UI shell in place; local SQL Server (Windows Auth, `localhost`) wired up; initial EF Core
  migration generated.
- 2026-11-09 — Dashboard, Instances list (filterable), instance create/edit form, instance detail +
  backup history + log-backup form, and Compliance CSV export pages built. App builds, runs, seeds
  demo data, and was visually verified (dashboard/instances/detail screenshots) via Playwright.
- 2026-11-09 — Published to https://github.com/architonixlabs/DBTracker under the MIT license
  (Architonix Labs LLP), free for public use with no restrictions.
- 2026-11-09 — Scope expanded mid-build: RBAC, shared-drive backup discovery, richer environment
  detail, notifications, and QR phone login requested. Added as Epics 5-9. Sequencing: RBAC first
  (everything role-based depends on it), then notifications, then shared-drive scan, then QR login
  (most complex — device-linking flow) last.
- 2026-11-09 — Further expanded: scheduled (interval-based) shared-drive scanning with visible
  last-scan timestamps, restore-event tracking (when/who/why/outcome), and scheduled management
  reports sent via configurable email (not locked to ArxMail — user wants Gmail/other SMTP
  supported too). Added as Epic 6 additions, Epic 6b, and Epic 10. User asked that future
  requirements be validated against personas as they come in, not just logged — doing that above.
- 2026-11-09 — Epic 5 (RBAC) built and verified end-to-end: ASP.NET Core Identity + roles wired into
  the shared DbContext, login/logout, role-gated UI and routes, demo users per persona. Found and
  fixed two real bugs surfaced while building the Playwright E2E coverage for it: (1) `HealthBadge`
  component wasn't imported app-wide, so health badges silently rendered as literal unrecognized
  tags; (2) the global auth fallback policy + `UseHsts()` + static web assets not being loaded
  outside Development combined to silently break Blazor Server interactivity (and framework JS)
  whenever the app ran unpublished outside the Development environment — fixed by scoping HSTS to
  Production, allowing anonymous static assets, and explicitly enabling static web assets outside
  Production. All 19 tests (12 unit + 7 E2E) pass.
- 2026-11-09 — Visual redesign pass (user flagged the prior UI as not attractive/professional, and
  asked for animations): new gradient/Inter design system, motion throughout, toast notifications,
  skeleton loaders. Found and fixed a real bug while wiring toasts: pages each declared their own
  `@rendermode InteractiveServer`, which made every full-page navigation start a fresh SignalR
  circuit/DI scope instead of one persistent circuit for the app — scoped services like the toast
  bus never saw the same instance twice. Fixed by setting the render mode once, globally, on
  `<Routes>` in App.razor and removing the redundant per-page declarations. All 19 tests still pass.
- 2026-11-10 — Published a visual design exploration (separate Artifact, not app code): an editorial
  ledger system — warm ink/paper palette, Fraunces/Inter type pairing, hairline-divided tables and
  underline form fields instead of boxed cards — across sign-in, dashboard, inventory, detail and
  compliance screens, per the user's ask for a "non-box", professional, multi-view design direction.
- 2026-11-10 — Built Epics 6, 6b, and most of 7: shared-drive backup scanning (manual "Scan now" +
  a configurable background sweep, file-name matched so re-scans never duplicate), restore-event
  logging with a Compliance "Restore tested" flag, and retention/tags/runbook fields on an instance.
  Added 5 unit tests (scanner matching logic) and 2 new E2E tests (configure a real temp folder as
  the "shared drive" and scan it; log a restore and verify it flags on Compliance). One E2E flake
  fixed along the way: a Windows path embedded in a CSS `:has-text()` selector had its backslashes
  parsed as CSS escapes — switched to Playwright's `HasText` locator option instead. All 26 tests
  (17 unit + 9 E2E) pass.
- 2026-11-09/10 — Built Epic 8 (notifications): configurable email (SMTP or ArxMail HTTP gateway,
  user asked it not be locked to one vendor), a background alert sweep that notifies on a health
  state change (not every tick), an in-app bell with unread badge, and — since it was a dangling
  nav link since the RBAC work — a minimal Admin Users page. Hit a real, fairly subtle bug building
  this: the notification bell renders on every page via the sidebar, and its own DB query ran
  concurrently with the page's own query against the one shared scoped DbContext — EF Core's "a
  second operation was started on this context instance" — which cascaded into widespread E2E
  timeouts (circuit effectively died on every page with the bell mounted). Fixed by giving
  `NotificationService` its own short-lived DbContext per call via `IDbContextFactory`, registered
  without re-triggering the earlier AddDbContext/AddDbContextFactory lifetime conflict (the scoped
  `DBTrackerDbContext` everything else injects is now itself created from the factory, so there's
  only one path configuring `DbContextOptions`). Also fixed an actual test bug along the way (not
  an app bug): a generated test email was truncated with `.Substring(0, 40)`, cutting it off before
  the `@`, so the create-user form's email validation silently blocked submission. 11 unit tests and
  all E2E tests added/fixed this round; full suite is 39/39 (28 unit + 11 E2E).
- 2026-11-10 — Built Epic 10 (management reporting): a scheduled (default weekly) + on-demand
  "Send report" compliance email, reusing Epic 8's email sender and the Compliance view's data.
  Report body built by a pure, unit-tested `ComplianceReportBuilder` (at-risk instances listed
  worst-first, restore-test coverage called out) kept separate from the I/O that fetches data and
  sends it, so the content logic didn't need a database to test. 5 new unit tests, 1 new E2E test
  (Auditor — not DBA — sends a report and sees the success toast, confirming the button is
  leadership-role-gated rather than DBA-gated). Full suite: 44/44 (32 unit + 12 E2E).
- 2026-11-10 — Built Epic 9 (QR phone login), the last of the originally requested epics: a desktop
  (unauthenticated) shows a QR code; a phone that's already signed in scans it, sees "Sign in as
  <email> (<role>)?", and approves or denies; the desktop polls and signs itself in automatically on
  approval. Deliberately never bypasses password auth — only an already-trusted phone session can
  vouch for a new device, same model as WhatsApp Web/Google's phone sign-in. Tokens are a
  cryptographically random 48-char hex string, 5-minute expiry, single-use (marked Redeemed
  immediately so a captured token can't be replayed). The desktop-facing endpoints (create/qr/
  status/redeem) are anonymous by necessity and reached only via JS `fetch()`, not a form, so they
  explicitly opt out of antiforgery rather than silently relying on it; the phone-facing approve/
  deny endpoints go through real forms with antiforgery tokens, same pattern as the existing Logout
  endpoint. Verified both by an automated two-browser-context E2E test (approve and deny paths) and
  by hand with real Playwright screenshots of the whole flow before trusting the automated version.
  Full suite: 46/46 (32 unit + 14 E2E). This closes out every epic from the original ask.
- 2026-11-10 — Two rounds of visual redesign on user feedback. First pass applied the `/design`
  artifact's editorial direction (warm paper background, Fraunces serif + Inter, terracotta/teal
  accents, hairline dividers instead of boxed cards). User then flagged that look as itself a
  recognizable "AI redesign" cliché and asked for something that reads as human-made for the actual
  audience. Replaced it with a dark ops-console theme instead: IBM Plex Mono for data/headings/nav,
  amber/green/red status LEDs with a glow instead of pill badges, a grid-paper texture background,
  `// eyebrow` comment-style section labels, and a literal terminal-window login card (title bar,
  `>` prompt-style fields, blinking cursor) replacing the generic split-screen hero. Found and fixed
  a real CSS bug along the way: Bootstrap's `.table` sets `background-color: var(--bs-table-bg)`
  (defaults to white) directly on every `td`/`th`, which only showed up on the two backup/restore
  history tables that had `dbt-table` on the `<table>` itself rather than a wrapping div — explicit
  `background-color: transparent` override added. Two E2E selectors (`.stat-card`/`.value`) updated
  to match the renamed `.stat-cell`/`.stat-value` markup. Full suite still 46/46 after both passes.
- 2026-11-10 — Fixed two real notification-bell alignment bugs the user reported: the blinking
  terminal cursor was attached to the whole sidebar brand row instead of the wordmark span, so it
  rendered after the bell icon instead of right after "DBTracker"; and the notification dropdown
  was positioned `right: -8px` relative to a bell sitting near the edge of the 240px sidebar, so the
  320px panel computed to `x: -114px` — over a third of it clipped off the left edge of the browser
  entirely. Confirmed both with Playwright bounding-box checks before and after, not just visual
  inspection (a full-page screenshot downscale artifact earlier in the session had looked like a
  bug — a white table-row background — when it wasn't; this time the bounding box proved it was real).
- 2026-11-10 — Added Epic 11 (Reports center): a `/reports` hub with four new report types
  (Backup Activity, Restore Testing, Ownership Roll-up, Scan Activity), each with CSV export, plus
  the existing Compliance Snapshot and Management Summary surfaced in the same place rather than
  duplicated. Validated against all four personas before building (see Epic 11's note) rather than
  guessing at report types. Aggregation logic factored into four new pure builders in
  `DBTracker.Core.Services` (`BackupActivityReportBuilder`, `RestoreTestingReportBuilder`,
  `OwnershipRollupBuilder`, `ScanActivityReportBuilder`) with 13 new unit tests. Also audited the
  whole backlog for staleness while in there: the Epic 5 "Users management page" item was marked
  undone though it had actually shipped (under Epic 8) — split into what's done (create + assign
  role) vs. what's genuinely still missing (deactivate / change an existing user's role); and the
  Design section still described the original indigo/violet gradient look two redesigns later —
  rewritten to describe the current dark ops-console theme and its two predecessors. Full suite:
  65/65 (45 unit + 20 E2E).

## Out of scope (future)
- Automated backup execution/orchestration (this tracks status, doesn't run backups)
- Full SSO/enterprise IdP integration (local Identity accounts for now)
- Email delivery for the four new Epic 11 reports (Backup Activity, Restore Testing, Ownership
  Roll-up, Scan Activity) — deliberately scoped to view + CSV export; only Management Summary emails
  (it reuses Epic 10's existing sender/distribution-list plumbing). Add if a persona asks for one
  scheduled/emailed on its own.
- Scheduled/recurring exports (e.g. Backup Activity auto-emailed weekly) — today every Epic 11
  report is generated on demand when the page is opened
- Report date-range beyond 7/30/90-day presets (custom from/to picker) on Backup Activity
