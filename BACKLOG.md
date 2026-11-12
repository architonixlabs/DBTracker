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
- [x] Deactivate an existing user / change an existing user's role from the Users page — currently
      create-only; changing a role today means editing seed data or the database directly
      (delivered by U16: role change, Deactivate/Reactivate on the Users page; see UPDATE-016)

### Epic 6 — Backup evidence from shared drive
*Serves Priya (DBA, who shouldn't have to hand-log every backup) and Angela (Auditor, who needs
proof it's real file evidence, not a self-reported checkbox).*
- [x] Per-instance configurable backup file location (UNC path / folder pattern)
- [x] Scanner that reads file name/timestamp/size at that location and auto-creates BackupRecord
      entries (`Source`: Discovered vs Manual), matched by file name so re-scans never duplicate
- [x] Scheduled recurring scan (`BackupScan:IntervalMinutes`, default 15; 0 disables it) plus a
      manual "Scan now" button on the instance page for an on-demand check
- [x] Last-scanned time and scan errors (path unreachable etc.) surfaced on the instance detail page
- [x] Surface staleness on the dashboard too (Epic 12 — banner when a scan is erroring or never completed)

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
- [x] Custom/extensible environment tags beyond the fixed tier (region, cluster, replica set)
      (delivered by U21: Region, Cluster and Replica set fields and facets; see UPDATE-016)
- [x] Audit trail of who changed what on an instance record (Epic 12 — "Change history" on the instance detail page)

### Epic 8 — Notifications
- [x] Configurable email sender (SMTP — Gmail, Office365, any relay — or the ArxMail Gateway's HTTP
      API; `Email:Provider` picks one, defaults to None which only logs) for alerts when an
      instance goes Overdue or Failed
- [x] In-app notification center: bell with unread badge in the sidebar, dropdown list, mark-all-read
- [x] Alerts fire on a *state change* into Overdue/Failed (not every sweep) via a background check
      (`Alerts:IntervalMinutes`, default 10) — re-alerts if it recovers and relapses later
- [x] Per-user notification preferences (which instances/companies they care about) — delivered
      under Epic 14 (U07): per-user subscriptions and personal read state, see below. Broadcast to
      everyone remains the default when a user has no subscriptions configured.
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
- [x] ~~Dark ops-console theme — IBM Plex Mono for data/nav/headings, Inter for body prose,
      amber/green/red status LEDs with a glow instead of pill badges, hairline dividers instead of
      boxed cards, grid-paper texture background, `// eyebrow` comment-style section labels, a
      terminal-window login card (title bar, `>` prompt fields, blinking cursor)~~: superseded by the
      2026-11-11 antislop pass (see Progress log)
- [x] ~~Motion: page fade-in, toast slide-in, skeleton shimmer loading states, blinking terminal
      cursor on the brand wordmark, pulsing glow on Failed indicators~~: superseded, see below
- [x] Current (2026-11-11): dark operations console, kept dark on purpose for long monitoring
      sessions. Inter for interface text, IBM Plex Mono only for data values (IDs, IPs, sizes,
      timestamps). Amber/green/red status lights are the identity motif; only Failed glows, and its
      pulse plays twice then stops. Flat background, plain sign-in panels, plain section titles. All
      text tokens meet WCAG AA; visible amber focus outline on every control. Motion: toast slide-in,
      loading indicators, the Failed pulse; all of it stops under `prefers-reduced-motion`.
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
- 2026-11-10 — Asked the user to evaluate further features; proposed a shortlist validated against
  all four personas and let the user pick rather than building everything unprompted. User selected
  seven: "My instances" view, instance change-log audit trail, dashboard scan-staleness indicator,
  maintenance windows/snoozed alerts, a TCP connectivity check, bulk CSV import, and generic webhook
  alerts. All seven shipped as Epic 12 (below). Along the way, found and fixed a real pre-existing
  bug the new audit-trail feature happened to be the first thing to exercise: `InstanceEditor.Save()`
  never called `Db.ChangeTracker.Clear()` after `SaveChangesAsync()` (every other mutating handler in
  the app does). Since the scoped DbContext persists across navigation within a Blazor Server
  circuit, creating an instance left it tracked as Unchanged; editing that same instance later in the
  same session then threw "another instance with the same key is already being tracked" when
  `Update()` tried to attach the freshly-loaded model — caught by `EditingInstance_RecordsChangeHistory`,
  diagnosed from the actual server-side exception log (not guessed), fixed, and the same `Clear()`
  added to the CSV import save path too. Full suite: 98/98 (70 unit + 28 E2E).
- 2026-11-10 — Built Epic 13 (U01-U04) from the usability roadmap: per-instance backup file
  name-pattern rules with a scan preview (eligible/excluded/already-recorded, with the reason) on
  the instance editor; evidence levels (Discovered / Reported success / Restore-tested — promoted
  only once a later successful restore actually proves the chain, never silently upgrading a bare
  file) shown per backup record on the instance detail page; stale-scanner detection
  (`ScannerStalenessClassifier`) separate from backup health, with a dashboard banner and
  Scan Activity report columns for next-expected-check and a recovery hint; and a "Health
  explanation" panel (last success, latest attempt, governing interval, next due, overdue-by,
  maintenance suppression called out separately) on the instance detail page. Dashboard,
  Instances list and the notification bell now show a last-updated time with manual refresh and a
  pausable 30s auto-refresh, reading through `InstanceOverviewService`'s own `IDbContextFactory`
  context (same isolation pattern as `NotificationService`) so a refresh never races a page's own
  mutations. Concurrent-scan-safe by construction: Discovered evidence is matched by file name
  *and* exact byte size (a new `ObservedFileBytes` column), guarded by a unique filtered DB index,
  so a changed file reusing an old name is recorded as new evidence instead of being silently
  skipped or duplicated, and two scans racing the same new file never double-insert. "Still
  changing" detection stats each candidate file twice, 250ms apart, rather than using a wall-clock
  recency guess. Found and fixed two real bugs along the way: `BackupHealthCalculator.Compute`
  didn't exclude future-dated records, so a backdated/clock-skewed record could make an earlier
  instant look healthier (or worse) than the evidence available at that instant actually supported
  — fixed by filtering to `TimestampUtc <= asOfUtc` before picking the most recent record, with an
  explicit Id-descending tiebreak for same-instant records; and the staleness threshold math
  originally used `Math.Max(1, scanIntervalMinutes)`, which meant the Testing environment's
  `BackupScan:IntervalMinutes: 0` (background scan intentionally disabled) floored to a 1-minute
  interval and flagged every seeded instance's decades-old scan as "Stale" — fixed so an interval
  of 0 or less is treated as manual-only scanning (no periodic schedule to go stale against),
  matching `BackupScanHostedService`'s own convention for that setting. 22 new unit tests
  (`BackupEvidenceClassifierTests`, `BackupFileFilterTests`, `ScannerStalenessClassifierTests`,
  plus additions to `BackupHealthCalculatorTests` and `ScanActivityReportBuilderTests`) and 5 new
  E2E tests (`EvidenceAndHealthExplanationTests`). Stale-scanner *UI* rendering is covered at the
  unit/report-builder level rather than E2E, since the Testing environment's manual-only scan
  config (needed to keep seeded fixture data from registering as falsely stale) makes a live
  "Stale" badge impractical to reproduce in the browser without a dedicated test server
  configuration — a gap worth closing if U02's UI regresses. Full suite: 141/141 (107 unit + 34 E2E).
- 2026-11-10 — Built Epic 14 (U05-U08) from the usability roadmap: shareable/saved filtered views,
  bounded list browsing, a personal notification inbox, and incident acknowledgement/escalation.
  **U05** — Instances-list filters/sort/page now live entirely in validated URL query parameters
  (`?engine=&health=&company=&dept=&owner=&tag=&mine=&sort=&dir=&page=`), so any filtered view is a
  plain shareable link and the browser back button restores the prior state; added dedicated
  Department/Owner/Tag facet dropdowns (previously only engine/health/role/env/company existed) and
  clickable column-sort headers. Per-user named "saved views" (`SavedView` entity) store that same
  query string and can be applied, renamed or deleted from a "Views" menu; the dashboard's stat
  cells and the scan-issue banner now deep-link into the same `/instances`/`/reports` query params
  rather than only filtering in place. **U06** — added `InstanceListRow`/`InstanceListFilter`, a
  pure, fully unit-tested projection+filter+sort+page engine in `DBTracker.Core.Services`, and a new
  `InstanceOverviewService.GetAllSummariesAsync`/`GetPagedAsync` that computes each instance's
  health from one correlated-subquery "latest record" and "latest successful record" pair instead
  of `Include`-ing every instance's entire BackupRecords/RestoreEvents graph — the Instances list
  and the Dashboard both moved onto it. The Instances list is now genuinely paged (25/page, stable
  sort, total count shown) instead of rendering every row. Per-instance Backup/Restore/Change
  history on the instance detail page gained real pagination and a status/outcome/field filter too,
  client-side over that one instance's own (much smaller) history — the big win was cutting the
  cross-instance dashboard/list queries down to summaries, so that's where the rework went; exports
  on the Reports page now say explicitly that they cover every matching row, not just what's
  rendered. **U07** — notification read state is now per-user (`NotificationReadState`), not a
  single shared `IsRead` flag: a user's first-ever visit to their inbox treats everything that
  already existed at that moment as already read (the one-time migration of the old shared/broadcast
  world, applied lazily per user rather than one big backfill), so nobody opens the bell to years of
  backlog marked unread, and only notifications created after that first visit are ever unread for
  them — reading or mark-all-reading never touches another user's state. Added per-user
  `NotificationSubscription`s (All instances / one instance / a company, each with In-app/Email/Both)
  and a `/notifications` page for the inbox + managing them; the bell now shows a personal count and
  deep-links each item to its instance. Subscriptions only ever narrow an already-broadcast alert —
  there's no per-instance row-level security in the app yet (that's U21), so a subscription can't
  grant visibility beyond what the viewer's role already sees. **U08** — a new `Incident` entity
  tracks assignment, acknowledgement (who/when), a note/ticket reference and escalation level for an
  at-risk instance, entirely separate from `BackupHealthCalculator` — acknowledging never changes
  the health badge, by construction, since nothing about acknowledgement feeds into the health
  calculation. `AlertCheckHostedService` now opens an incident the moment an instance becomes
  alertable (re-using the existing one rather than duplicating if it's already open — including
  across a maintenance window, so a still-bad instance correctly re-alerts post-maintenance without
  a second incident), closes it on recovery (with a "recovered" notification), and escalates
  (`IncidentEscalationEvaluator`, pure/unit-tested) on a configurable interval
  (`Alerts:EscalationIntervalMinutes`) until someone acknowledges it. `NeverBackedUp` is now
  alertable too (it previously never notified at all), gated by a configurable onboarding grace
  period (`Alerts:NeverBackedUpGraceHours`, default 24h) so a database added minutes ago doesn't
  immediately page anyone — this is an intentional behavior change, and the existing
  `AlertEvaluatorTests` were updated accordingly rather than left asserting the old behavior. Since
  `AlertCheckHostedService` itself is disabled in the Testing environment (same as every prior
  alert-dependent feature), `SeedData` now also seeds one open incident on the already-seeded Failed
  instance so acknowledgement has something real to exercise end-to-end. One new EF migration
  (`AddSavedViewsSubscriptionsIncidents`) for the four new tables plus a `Company` column on
  `Notification` (subscription matching needs it, the same way `InstanceName` was already
  denormalized onto it). 29 new unit tests; 6 new/changed E2E tests across three new files
  (`SavedViewsAndPagingTests`, `NotificationPersonalInboxTests`, `IncidentAcknowledgementTests`) plus
  a rewrite of the one existing notification-bell E2E test whose premise (counting unread seed data
  against a shared `IsRead` flag) no longer held once reads became personal. Full suite: 176/176
  (136 unit + 40 E2E).
- 2026-11-10 — Built Epic 15 (U09-U12) from the usability roadmap: report scope and delivery
  outcomes are now explicit — "Send report"/"Send now" open a preview (generation time,
  recipients, row count, and an explicit "current filtered view vs. full inventory" scope choice
  on Compliance) before anything is sent, and `IEmailSender.SendAsync` now returns a real
  Disabled/Accepted/Failed outcome per recipient instead of the old boolean-ish "count of
  configured recipients" that silently claimed success even when the email provider itself was
  disabled (`Email:Provider=None`) — found while building this: the pre-existing "Send report"
  E2E test was asserting that exact bug (a "Report sent" toast when nothing was actually sent in
  the Testing environment), so it was rewritten to assert the honest outcome instead. Compliance
  snapshots are now persisted (`ComplianceSnapshot` + `ComplianceSnapshotBuilder`, frozen rows as
  JSON) via a new "Save snapshot" action and a `/compliance/snapshots/{id}` detail page that
  reproduces exactly what was saved regardless of later edits/archiving/backdating; Backup
  Activity gained a Custom from/to date range alongside the 7/30/90-day presets. Backup and
  restore log entries now separate occurrence time (user-set, backdatable, future times
  rejected) from entry time and the authenticated recorder's identity (`RecordedByUserName`),
  and support append-only corrections (`BackupRecordCorrection`/`RestoreEventCorrection` +
  `EvidenceCorrectionResolver`) that never overwrite the original record — a correction layers a
  newer effective value on top, shown with a "(corrected)" marker. Restores can now optionally
  link to the specific backup evidence they proved, with duration/target-recovery-time fields.
  Restore-test recency (`RestoreTestRecencyCalculator`, org-wide `RestoreTest:IntervalDays`
  default with a per-instance override) is reported as Never tested/Current/Overdue — distinct
  from ordinary backup health — on the Restore Testing report and the instance detail page. A
  second real bug was found and fixed while writing a correction-panel E2E test: the "Save
  correction" button's disabled state was gated on a plain `@bind` text field, which (correctly,
  per Blazor's default) only updates on blur/change, so clicking the button immediately after
  filling the field in the same interaction left it stuck disabled — fixed with
  `@bind:event="oninput"` so it enables as the reason is typed, matching how a DBA would actually
  expect it to behave. U19 (a unified audit log) doesn't exist yet, so the snapshot's
  actor/time/scope metadata is self-contained for now rather than integrated with it. 26 new unit
  tests and 5 new E2E tests; one pre-existing E2E test's locator was scoped with `.First` after a
  second `.badge-health` element (the new restore-test-coverage badge) legitimately appeared on
  the instance detail page. Full suite: 168/168 (129 unit + 39 E2E).
- 2026-11-10 — Merged Epic 14 and Epic 15, which were built in parallel from the same Epic-13 base
  (Epic 14 directly on main, Epic 15 in an isolated worktree). Reconciled conflicts in
  `DBTrackerDbContext`, the EF Core migrations snapshot (both epics' migrations now apply in
  sequence against one consistent model), `AlertCheckHostedService`, `InstanceDetail.razor`, and
  this backlog file (both sets of checkboxes and both progress-log entries above are preserved).
- 2026-11-10 — Built Epic 16 (U13-U19) from the usability roadmap: safer entry/recovery and
  administration. **U13** — a shared `InstanceValidator` (required fields, storage lengths,
  positive intervals, nonnegative measurements, http/https-only link schemes, enum values) is now
  used by both the create/edit form and the CSV importer; the editor shows field-level + summary
  errors, warns on navigating away with unsaved changes (`NavigationManager.
  RegisterLocationChangingHandler` plus a real `beforeunload` prompt for an actual tab close),
  guards duplicate submits, preserves input after a failed save, detects a concurrent edit via a
  new `RowVersion` concurrency token with an explicit reload-or-overwrite choice, and shows a clean
  not-found state for a bad/missing instance id (instance detail too, not just the editor).
  **U14** — archiving now requires a preview (`ArchivePreviewBuilder`: scan/alert/incident/report
  impact) and a reason, records actor/time/reason on the instance itself and in the new U19 audit
  log, and a new `/instances/archived` browser (Admin/DBA) searches and unarchives; nothing about
  health/alert eligibility is cached anywhere, so unarchiving just means the next read/sweep stops
  excluding the instance, and the instance detail page flags a direct link to an archived instance.
  **U15** — a new Admin-only `/setup` page: an import/create/email/scan/alert/report-enablement
  checklist, each background worker's last/next run via a new `WorkerHeartbeatStore`, backup-file-
  path reachability checked under the service's own identity, and a real send-test-email action
  reusing U09's Disabled/Accepted/Failed outcome rather than a fake success; credentials are
  masked. **U16** — the Users page gained role change, deactivate/reactivate (guarded so the last
  active Admin can't be removed), a self-service change-password page, and expiring single-use
  reset/invite links built on ASP.NET Core Identity's own password-reset token (its security-stamp
  rotation on success makes it genuinely single-use, so no second token table was needed) sent
  through the existing email sender; a forced `MustChangePassword` flag routes a new/reset account
  to the change-password page on first sign-in; deactivation rotates the security stamp (revoking
  sessions within the 30-minute revalidation window) and denies any pending, unredeemed device-link
  approval; `/Account/ForgotPassword` gives the same neutral response whether or not the account
  exists, with actionable guidance when email delivery is disabled. **U17** — `InstanceCsvImporter`
  now matches a re-imported row by Name (case-insensitive) and classifies each as
  Create/Update/Skip/Ambiguous (a name repeated in-file, or matching more than one existing row),
  reuses U13's validation, and its CSV tokenizer was rewritten to handle quoted commas and embedded
  newlines properly instead of splitting on raw newlines first; a new bulk-edit page lets Admin/DBA
  change owner/tags/retention across a selection with a before/after diff preview and a count,
  re-checks for archived instances at apply time, and deliberately has no path to archive. **U18**
  — `ConnectivityChecker`'s caller now resolves an optional per-instance host/DNS and port override
  (`ConnectivityEndpointResolver`; an invalid port falls back to the engine default rather than
  being used as-is); the instance page shows the actual host:port probed, when it was checked, the
  timeout, and states plainly that a TCP connect isn't proof of database login or backup health;
  the button disables when there's no host/IP/override to check at all. **U19** — a new
  `AuditEvent` table/`AuditRecorder` (System vs. User actor, action/target/reason/before-after,
  strictly append-only — there is deliberately no Update/Delete method on the recorder) now covers
  instance create/edit, archive/unarchive, maintenance windows, CSV import/bulk edit, account
  changes and manual evidence corrections; a new `/audit` page (Admin/Auditor) is searchable and
  CSV-exportable. U10/U11's own self-contained actor/timestamp capture (noted when they shipped,
  see the Epic 15 entry above) was deliberately left alone rather than retrofitted onto this.

  Found and fixed three real bugs building this. Most serious: a CSV re-import covering only a few
  columns (e.g. a lean re-export of Name/Engine/Application) silently blanked every column it
  didn't include — including contact email — on the matched existing instance, because the
  importer always built a full replacement row from column lookups that default to empty. Caught
  by a new E2E test for U17's own "repeat import updates, doesn't duplicate" claim, which
  incidentally also shrank Priya's "My instances" count in an unrelated pre-existing test; fixed so
  an Update row only overwrites a field whose column is actually present in the file's header,
  falling back to the existing value otherwise — a present-but-empty cell still clears the field,
  only a genuinely absent column doesn't. Second: this app's global `RequireAuthenticatedUser()`
  authorization fallback policy (added under Epic 5) also caught the interactive-render-mode's own
  transport endpoints (`/_blazor/initializers`, the SignalR hub), which carry no authorization
  metadata of their own — redirecting them to the login page instead of returning the JSON/
  handshake the Blazor JS runtime expects, which silently broke the interactive circuit on any
  genuinely anonymous page relying on normal C# event dispatch rather than a named-form SSR
  post-back the way Login.razor's form happens to be built (the first such page is this round's
  new Forgot/Reset password flow). Fixed by making every page's own `[Authorize]`/`[AllowAnonymous]`
  attribute the sole source of truth — added an explicit `[Authorize]` to the handful of pages
  (Home, Compliance, Reports, Notifications, the Instances list/detail, Error, not-found) that had
  been relying on the implicit fallback — and removing the fallback policy entirely, re-verified
  against the existing DBA-blocked-from-`/users` E2E test so this didn't quietly loosen any role
  gate. Third (a test-writing bug, not an app bug, same class as the one called out in the Epic 15
  entry): the Setup page's "Send test email" button's disabled state was gated on a plain `@bind`
  field that only updates on blur, so filling it and clicking immediately left it stuck disabled —
  fixed with `@bind:event="oninput"`. 55 new unit tests and 9 new E2E tests
  (`Epic16ArchiveAndDiagnosticsTests`, `Epic16AccountLifecycleTests`, `Epic16ImportAndBulkEditTests`);
  one pre-existing E2E test's CSV-import assertions were updated for the new create/update/skip/
  ambiguous preview wording. Full suite: 267/267 (213 unit + 54 E2E).
- 2026-11-10 — Built Epic 17 (U20-U22), the last epic of the usability roadmap. **U20** — within the
  existing dark ops-console theme (no redesign): instance rows gained a real `<a>` link alongside the
  existing row-click (semantic navigation, not mouse-only); column-sort `<th role="button">` cells
  became real `<button>` elements (keyboard-operable by default, with `aria-label`s); the notification
  bell and the Instances "Views" dropdown gained `role="menu"`, Escape-to-close, and focus returned to
  their toggle button on close; every filter-bar input got a real (visually hidden) `<label>`; toasts
  gained `role="status"`/`role="alert"` + `aria-live` so save/error feedback is announced, not just
  shown; `:focus-visible` outlines, a scrollable `role="region"` wrapper on dense tables, and a 480px
  breakpoint (stacked topbar/filters, 44px touch targets) cover the 360px/200%-zoom and touch-target
  acceptance criteria without touching the reduced-motion support or status-text conventions already
  in place. **U21** — a `Team`/`TeamMembership` model (explicit user-to-team membership) and a
  `BackupContactName/Email/Phone` pair on `DatabaseInstance` extend ownership beyond one contact email
  while leaving the existing `ContactName/Email/Phone` fields untouched; `OwnershipStatusClassifier`
  (Missing/Unconfirmed/Confirmed) and a confirm-ownership checkbox on the editor surface
  missing/unconfirmed owners distinctly; `Region`/`Cluster`/`ReplicaSet` structured fields sit
  alongside the existing free-text `Tags` as new Instances-list facets; a reusable, version-bumping
  `BackupPolicy` entity can be applied to selected instances via a new `/instances/apply-policy` page
  that previews the exact before/after diff first (mirroring U17's bulk-edit preview pattern) and only
  writes on explicit confirmation; a `ContactCorrectionRequest` flow lets any authenticated viewer
  (including a Viewer-role Dev persona) flag wrong contact info for an authorized (`CanEditPolicy`)
  reviewer to approve/reject, applying only the proposed fields that were actually filled in. A new
  `/ownership` hub (Teams / Backup policies / Correction requests) and `InstanceListFilter.IsMine`
  (email OR backup-email OR team membership) tie it together; `NotificationSubscription` gained a
  `Team` scope on the same narrowing-only basis as the existing `Company`/`Instance` scopes. Every
  addition is explicitly documented as personalization, not row-level security, matching how U07
  already phrased it. **U22** — a new "Trends" tab on `/reports` compares two user-chosen periods
  using the nearest U10 `ComplianceSnapshot` at or before each period's end for health/restore-tested
  coverage (a period with none is flagged "missing evidence" rather than silently computed from
  today's live inventory), plus raw dated `BackupRecord`/`RestoreEvent`/`Incident` rows for repeated
  failures, backup duration/size, restore-test counts and outstanding-risk age (via `Incident.
  OpenedAtUtc`, a real persisted timestamp, not a recomputed status) — all built by a new pure
  `TrendsReportBuilder`. Every period shows its inventory denominator explicitly, at-risk/repeated-
  failure/outstanding-risk rows are drillable into their own accessible tables (U20's `role="region"`
  pattern), and the tab states plainly that retention-days values aren't evidence files still exist on
  disk (that's D17, future DBA-lifecycle scope). Found and fixed one real recurring-class bug while
  building this, the same one called out in the Epic 15 and Epic 16 entries above: the new "Create
  team"/"Create policy" inputs and the contact-correction form's proposed-field inputs used a plain
  `@bind` (blur-triggered), so filling and clicking at Playwright speed left their buttons stuck
  disabled — fixed with `@bind:event="oninput"` everywhere a button's enabled state reads one of
  these fields. Also hardened two E2E-only issues that aren't app bugs: a handful of new tests
  navigated by clicking a table row, which occasionally hung on Playwright's implicit post-click
  navigation wait under Blazor's enhanced (non-full-page-load) navigation — switched to navigating via
  the new semantic `a.instance-row-link`'s own `href`; and a fast fill+click on "Create team" could
  register two toast events from pure test-automation timing (never reproduced from one real click) —
  toast assertions now target `.First`. 32 new unit tests (`OwnershipStatusClassifierTests`,
  `BackupPolicyApplyPreviewBuilderTests`, `ContactCorrectionApplierTests`, `TrendsReportBuilderTests`,
  plus additions to `InstanceListFilterTests`/`NotificationSubscriptionMatcherTests`) and 10 new E2E
  tests (`Epic17AccessibilityTests`, `Epic17OwnershipTests`, `Epic17TrendsTests`). One new EF migration
  (`AddUsabilityEpic17`) for `Team`/`TeamMembership`/`BackupPolicy`/`ContactCorrectionRequest` plus the
  new `DatabaseInstance`/`NotificationSubscription`/`Notification` columns. Full suite: 308/308
  (244 unit + 64 E2E). This closes out the usability roadmap (Epics 13-17); the DBA lifecycle roadmap
  (Epics 18-22) remains proposed and unimplemented.

### Epic 12 — Handiness features (user-prioritized)
*Every item below was proposed as a shortlist and picked by the user, not decided unilaterally.*
- [x] "My instances" view — a "My instances only" checkbox on the Instances list, scoped to the
      logged-in user's contact email; only shown at all if they're actually the contact on at least
      one instance (Admin/leadership roles that own nothing don't see a dead-end filter). Closes the
      original Dev/app-owner persona's "simple view scoped to my applications" ask from Epic 1 that
      nothing had actually delivered yet.
- [x] Instance change-log audit trail — every edit through the instance form diffs the before/after
      state field-by-field (`InstanceChangeLogBuilder`, pure/unit-tested) and records who/when/what
      changed; shown as a "Change history" table on the instance detail page. Serves Angela
      (Auditor) — who changed what, not just that a backup ran.
- [x] Dashboard scan-staleness indicator — a banner on the dashboard when any shared-drive scan is
      erroring or has never completed, linking to the Scan Activity report. Closes the Epic 6 gap
      ("surface staleness on the dashboard too") from a session ago.
- [x] Maintenance windows — start a 1h/4h/24h window (with a reason) from the instance detail page;
      Overdue/Failed email and webhook alerts are suppressed for the duration (`AlertEvaluator.
      IsSuppressedByMaintenance`), but the health badge itself stays truthful and a small wrench icon
      on the dashboard/list rows explains why a Failed instance isn't alerting.
- [x] Connectivity check — a "Test connection" button on the instance detail page does a plain TCP
      reachability probe (`ConnectivityChecker`) against the server/database IP on the engine's
      conventional default port (`DefaultPortMapper`); proves the host:port accepts a connection, not
      that the database itself is healthy — deliberately no per-engine driver dependency.
- [x] Bulk CSV import — `/instances/import` (Admin/DBA, same policy as Add instance): upload a CSV,
      preview valid rows and per-row errors before committing, download a template. Parsing/
      validation is a pure, unit-tested builder (`InstanceCsvImporter`), only Name and Engine
      required.
- [x] Generic webhook alerts — `Alerts:Webhook:Url` (+ optional `:Secret` header) sends the same
      Overdue/Failed alert as a plain JSON HTTP POST alongside email, payload built by
      `WebhookPayloadBuilder` with a top-level "text" field so Slack incoming webhooks (and most
      other generic receivers) work without platform-specific code. Disabled (logs instead) when no
      URL is configured, same pattern as the email sender.
- 2026-11-10 — Built Epic 18 (D01-D06), the first epic of the DBA lifecycle roadmap. **D01** — a new
  `VersionIdentity` (one per instance, layered on `DatabaseInstance.Version`, which is left untouched)
  holds normalized family/major/minor/build, edition/distribution/patch-train, hosting provider/
  context, and explicitly separates HostingEngineVersion from DatabaseCompatibilityLevel per the
  review's distinction; a new `ClusterNodeVersion` list tracks per-node versions within a cluster,
  deliberately kept apart from Epic 17's Region/Cluster/ReplicaSet topology facets rather than
  duplicating them. `VersionIdentityParser` (pure, engine-specific — PostgreSQL, SQL Server's
  marketing-year and internal-build forms, MySQL including a MariaDB-mislabel-as-NeedsReview case,
  MongoDB, Oracle's "19c" and legacy dotted forms) normalizes a raw string or marks it Unresolved/
  NeedsReview; an unrecognized string is never silently treated as a known, compliant product.
  **D02** — `ReleaseCatalogEntry`/`ReleaseCatalogRevision` curate GA/preview/channel/patch releases
  with source reference, date precision/timezone, TBD/conflicting flags, and append-only revision
  history; manual overrides require a reason, reviewer and optional expiry. `ReleaseCatalogService`
  never calls a live vendor feed — `RefreshAsync` only re-confirms a configured source and, on
  failure, leaves every existing fact untouched and sets only `IsStale`/`LastRefreshError`. **D03** —
  `SupportRunwayCalculator` (pure) walks Mainstream/Extended/ESU milestones to a phase and
  days-to-next-milestone, going negative (overdue) rather than disappearing once a milestone passes,
  and returns an explicit Unknown for TBD/conflicting/missing data; `SupportRunwayNoticeEvaluator`
  (pure) dedupes/escalates configurable 365/180/90/30-day notices so a sweep only re-notifies on a
  *more* urgent threshold, with overdue always counting as more urgent than any positive one.
  `SupportRunwayService.EvaluateAndNotifyAsync` routes notices through the existing U07 notification/
  subscription plumbing and is reachable both from a new disabled-by-default
  `SupportRunwayCheckHostedService` (`SupportRunway:IntervalMinutes`, matching the AlertCheckHostedService
  pattern) and an explicit "Evaluate now" admin action, so the same dedupe state and routing logic is
  exercised either way; `SupportRunwayDecision` links a kept action (Upgrade/Patch/Replace/Retire/
  Exception) with a text/reference field, full workflow entities being later-epic scope. **D04** —
  `PatchBaselineComparer` (pure) reports Installed, latest applicable same-branch patch, newest
  relevant GA and the approved organizational target as four separate facts; previews are excluded
  by default, and the approved target only ever comes from an explicit, reviewed, non-expired
  `ApprovedPatchBaseline` — it is never derived from "newest GA" automatically — with
  `IsMajorUpgradeFromApproved` distinguishing a same-branch patch from a major migration.
  **D05** — `AdvisoryRecord`/`AdvisoryInstanceAssessment` track imported advisories and their
  per-instance applicability; `AdvisoryApplicabilityClassifier` (pure) can only ever suggest
  Potential/NotApplicable/Unknown from a build-range match — Confirmed requires a human reviewer
  with evidence, since version matching alone cannot confirm vulnerability (distribution backports
  and managed-service patching both complicate it). **D06** — manual attestation
  (`VersionIdentityService.AttestAsync`) and a documented observation-import path
  (`VersionObservationService`) are the shipped first-class routes; `VersionDriftDetector` (pure)
  compares a declared identity against an observation and is always Unknown (never Drift) on a
  failed/denied observation attempt, and a failure is recorded without ever touching the declared
  identity or the instance's archived state. `VersionObservationSource.FutureCollector` exists only
  as a labelled extension point — no real network/credentialed collector was built. A new
  per-instance `/instances/{id}/lifecycle` page (linked from instance detail) covers identity entry/
  attestation, support runway, patch-baseline comparison and advisory review; a new admin
  `/lifecycle` hub (Admin/DBA) covers the catalogue, approved baselines and advisory import. One new
  EF migration (`AddDbaLifecycleEpic18`) for the nine new tables. Found and fixed one real migration
  bug while generating it: `ClusterNodeVersion`'s FK to `VersionIdentity` defaulted to `SetNull`,
  which SQL Server rejected ("may cause cycles or multiple cascade paths") because
  `DatabaseInstance` already cascades to both `VersionIdentities` and `ClusterNodeVersions` directly
  — fixed to `NoAction`, the same resolution already used elsewhere in this schema for an identical
  shape (`RestoreEvent.LinkedBackupRecord`). 82 new unit tests (`VersionIdentityParserTests`,
  `VersionBuildComparerTests`, `SupportRunwayCalculatorTests`, `SupportRunwayNoticeEvaluatorTests`,
  `PatchBaselineComparerTests`, `AdvisoryApplicabilityClassifierTests`, `VersionDriftDetectorTests`,
  `ReleaseCatalogRefreshEvaluatorTests`) and 7 new E2E tests (`Epic18LifecycleTests`) covering
  entering/viewing an unresolved version string, resolving-and-attesting a recognized one, a seeded
  approaching-deadline support-runway notice (and its downstream notification), the four-fact patch-
  baseline comparison, an advisory classification-then-review flow, and a catalogue manual-
  override/refresh-failure pair. Full suite: 397/397 (326 unit + 71 E2E). This completes Epic 18
  (D01-D06) in full; PLAN-001's lifecycle-only D15 is Epic 20 scope and was not touched here. Epics
  19-22 (D07-D22) remain proposed and unimplemented.
- 2026-11-10 — Built Epic 19 (D07-D10), the second epic of the DBA lifecycle roadmap, per PLAN-001's
  sequencing ("Then D07-D14 migration readiness..."). **D07** — a new `InstanceDependency` records
  what an instance relies on or who relies on it (drivers, extensions/plugins, jobs, linked services,
  replicas/HA groups, backup tooling, OS/runtime and managed-provider constraints), reusing Epic 17's
  `Team` for ownership rather than a new model; `DependencyImpactAssessor` (pure) summarizes unresolved
  dependencies and downstream consumers, surfaced as a banner on a new per-instance `/instances/{id}/
  migration` page before a shared-engine change — manual mapping only, no auto-discovery. **D08** — a
  curated, reviewed `PathSupportRule` set (managed from a new "Path rules" tab on the existing
  `/lifecycle` hub) and per-instance `PathAssessment` records; `PathSupportClassifier` (pure) matches
  source/target product+build+edition+platform+topology plus assessment type (version upgrade /
  relocation / cross-engine conversion, kept as distinct types) and method against the rule set,
  reusing Epic 18's `VersionBuildComparer` for build-range matching — the absence of a matching rule
  is always Unknown, never Supported, and conflicting rules also resolve to Unknown rather than an
  arbitrary pick. No executable migration scripts are generated and no downtime-free assumption is
  made anywhere. **D09** — `ReadinessChecklist`/`ReadinessCheckItem` cover driver/ORM, application-
  vendor certification, extensions, deprecated features, collation/encoding, OS, edition features,
  backup tooling and engine compatibility state (reusing D01's `DatabaseCompatibilityLevel` field, not
  a new rule); every check starts Unknown and `ReadinessChecklistAggregator` (pure) summarizes
  passed/failed/unknown/n-a counts plus the full blocking-finding detail rather than hiding it behind
  a count. A changed migration target (`ReadinessChecklistService.UpdateTargetAsync`) explicitly
  invalidates every already-recorded check back to Unknown, and expired evidence
  (`EvidenceExpiresAtUtc`) does the same purely by comparison against "now" — neither ever promotes a
  check to Passed. **D10** — `MigrationCampaign` groups affected deployments (`CampaignInstanceLink`)
  with accountable DBA/app/platform `Team` owners, blockers, milestones, effort/cost estimates and a
  change-ticket reference; `CampaignAlternative` records and compares upgrade/patch/replace/retire/
  support-extension-or-exception options side by side rather than assuming upgrade is the only path,
  and `CampaignApproval` is an append-only approval history. State only ever moves through
  `CampaignStateTransitionValidator`'s (pure) allowed graph — Proposed → Assessing → Ready → Scheduled
  → In progress → Validating → Completed, with Blocked/Rolled back/Cancelled as explicit exits — and
  `CampaignService.TransitionAsync` refuses an invalid jump with a reason rather than writing it;
  reaching any state, including Scheduled, never touches `PathAssessment`/`ReadinessChecklist`/
  `SupportRunwayDecision`, so scheduling a campaign cannot itself change or imply a reduced risk/
  readiness status recorded elsewhere, by construction. All four items' mutations go through the
  existing U19 `AuditRecorder`. Found and fixed one real migration bug while generating the EF Core
  migration: giving `MigrationCampaign`'s three separate team-owner foreign keys (`AccountableDbaTeam`/
  `AccountableAppTeam`/`AccountablePlatformTeam`) `SetNull` each triggered the same SQL Server "multiple
  cascade paths" restriction (error 1785) as the already-known shared-table case from Epic 18, just
  from three distinct columns on one table referencing the same `Teams` table rather than two tables
  chaining through each other — fixed to `NoAction` on all three (there is no team-delete feature in
  this app, so this never blocks a real operation), the same resolution already used for
  `RestoreEvent.LinkedBackupRecord` and `ClusterNodeVersion.VersionIdentity`. One new EF migration
  (`AddDependencyAssessmentEpic19`) for the ten new tables. 53 new unit tests
  (`DependencyImpactAssessorTests`, `PathSupportClassifierTests`, `ReadinessChecklistAggregatorTests`,
  `CampaignStateTransitionValidatorTests`) and 4 new E2E tests (`Epic19MigrationPlanningTests`)
  covering recording an unresolved dependency and seeing it flag before a shared-engine change, a
  path assessment showing Unknown with no matching rule and then Supported once a reviewed rule is
  added, a readiness checklist with a recorded Failed/blocking check alongside untouched Unknown ones,
  and a migration campaign comparing two alternatives, choosing one, transitioning state and recording
  an approval. Full suite: 454/454 (379 unit + 75 E2E). This completes Epic 19 (D07-D10) in full;
  Epics 20-22 (D11-D22) remain proposed and unimplemented.
- 2026-11-10 — Built Epic 20 (D11-D15), the third epic of the DBA lifecycle roadmap. **D11** — a new
  `RehearsalRecord` (+ `RehearsalRollbackDecisionPoint` children) attaches named source/target,
  representative-dataset, method, duration, backup-restore/correctness/performance outcomes, target
  vs. measured RPO/RTO, prerequisites, recovery/key dependencies, and a `PostCutoverWriteDisposition`
  reconciliation plan to an instance/campaign pairing, with an optional link to an existing U11/U12
  `RestoreEvent` rather than a duplicate evidence model; pure `RehearsalEvidenceEvaluator` computes
  Missing/Stale/Failed/Passed and feeds D13's completion gate. **D12** — `ChangeWindow` (+
  `ChangeWindowRunbookStep`/`ChangeWindowApproval`/`CutoverJournalEntry`) plans a timezone-labelled
  future window with outage estimate, communication owner, required approval scopes, runbook steps,
  and an append-only cutover journal; pure `ChangeWindowConflictDetector` checks overlap against
  sibling windows and Epic 12's existing maintenance windows (reported, never silently treated as
  safe); pure `ChangeWindowApprovalAuthorizer`/`ChangeWindowApprovalGate` enforce an explicit
  role/scope check before a window advances past Planned, with a D14 emergency-exception bypass —
  creating or editing a window never executes or notifies anything by itself. **D13** —
  `ChangeValidation` (+ `ChangeValidationSignoff`) captures observed build/compatibility state
  (reusing D01), application tests, integrity checks, a performance comparison, jobs/permissions,
  HA/replication, and a new backup/restore-evidence requirement, plus before/after evidence and a
  rollback-retention deadline; pure `ChangeCompletionGateEvaluator` requires every evidence field
  Passed, both DBA and Application-owner sign-offs Approved, and current/passing D11 rehearsal
  evidence — it never reads `DatabaseInstance.Version`, so a bare version-text edit cannot complete a
  change by construction. Wired into `CampaignService.TransitionAsync`: reaching `Completed` now runs
  the gate across every linked instance and refuses with the aggregated reasons unless an active,
  campaign-scoped D14 exception exists. **D14** — a generic `ReadinessException` (TargetType +
  TargetId + WaivedCheckReference, not hardcoded to one feature) records reason, compensating
  controls, accountable owner, a separate preparer/approver where policy requires it, and an
  expiry/review date; `IsVendorSupportCoverage` is always false — an internal waiver never changes
  the underlying vendor facts (D02/D03) or clears real risk; pure `ReadinessExceptionEvaluator`
  derives Active/Expired/Revoked/Rejected/PendingApproval live from status + expiry, so every gate
  re-blocks the instant the clock runs out without depending on a sweep; a disabled-in-Testing
  `ReadinessExceptionExpiryCheckHostedService` sends a one-time owner notice on expiry, and
  revoke/renew preserves history via `RenewalOfExceptionId`. **D15** — a new `/lifecycle/workspace`
  page + `LifecycleWorkspaceBuilder`/`LifecycleWorkspaceFilter`/`LifecycleWorkspaceSummary` and
  `LifecycleReportBuilder` (pure) project one row per instance (cluster nodes counted, never expanded
  into extra rows) with engine/branch, support phase, patch-baseline coverage, a new
  `DatabaseInstance.Criticality` field (distinct from environment tier), owner, campaign state,
  expiring-exception count, and explicit denominator/unknown-identity/unknown-support-phase counts;
  the CSV export reads from the same `AllRows`/`MatchingRows` pair the page renders, so it can't drift
  from what's shown. Found and fixed one real bug while building the change-window conflict preview:
  `CampaignChangeWindows.razor`'s "Check conflicts" button computed the candidate window's UTC instant
  with `_start.ToUniversalTime()` (converts the `datetime-local` value as if it were local time), while
  the actual save path used `DateTime.SpecifyKind(_start, DateTimeKind.Utc)` (treats the typed value
  as the literal UTC instant) — the two disagreed by the host machine's UTC offset, so a genuinely
  overlapping second window could be previewed as "no conflict" and then actually collide on save;
  fixed by making the preview use the same `SpecifyKind` interpretation as `CreateAsync`, caught by a
  new E2E conflict-detection test. One new EF migration (`AddEpic20RehearsalChangeManagement`) for the
  rehearsal/change-window/validation/exception/criticality tables, continuing the `NoAction`-on-shared-
  parent-table pattern from Epics 18-19 for `RehearsalRecord`/`ChangeValidation`'s instance FKs and
  `ReadinessException`'s self-referencing renewal FK. 78 new unit tests
  (`RehearsalEvidenceEvaluatorTests`, `ChangeWindowConflictDetectorTests`,
  `ChangeWindowApprovalAuthorizerTests`, `ChangeWindowApprovalGateTests`,
  `ChangeCompletionGateEvaluatorTests`, `ReadinessExceptionEvaluatorTests`,
  `ExceptionApprovalAuthorizerTests`, `LifecycleWorkspaceBuilderTests`, `LifecycleReportBuilderTests`)
  and 5 new E2E tests (`Epic20RehearsalCutoverTests`) covering a failed rehearsal blocking campaign
  completion, a detected change-window conflict, campaign completion blocked-then-allowed as evidence
  accrues, creating then revoking/renewing a readiness exception, and the lifecycle workspace's
  filtered/denominator-labelled/exported view. Full suite: 537/537 (457 unit + 80 E2E). This completes
  Epic 20 (D11-D15) in full; Epics 21-22 (D16-D22) remain proposed and unimplemented.
- 2026-11-10 — Built Epic 21 (D16-D20), the fourth epic of the DBA lifecycle roadmap. **D16** — a new
  `RecoveryObjective` (business RPO/RTO targets, plus lightweight encryption-key/offsite-copy
  reference notes — full tracking stays in D20/D17) and `BackupChainRequirement` rows (only the chain
  members an engine's topology actually requires — Base/Differential/TransactionLog/WalArchive/
  Binlog/Archival; recording none at all is what keeps coverage honestly Unknown instead of forcing a
  chain concept onto an engine that doesn't use one) feed pure `BackupChainCoverageCalculator`, which
  computes a recoverable point only up to a satisfied Base — a fresh log/WAL/binlog record is never,
  by itself, treated as proof of a valid base or end-to-end recoverability, however recent it is;
  measured restore duration is read straight from the existing U11/U12 `RestoreEvent` rather than
  re-entered. **D17** — `BackupCopyLocation` tracks primary/offsite/immutable-flagged storage
  locations with `FirstDiscoveredAtUtc` (historical, U01-style) kept explicitly separate from
  `VerificationStatus`/`LastVerifiedAtUtc` (current-state); pure `BackupCopyAvailabilityEvaluator`
  treats a merely-discovered-once, never-or-stale-verified copy as not currently available, and pure
  `RetentionCoverageComparer` only counts currently-accessible copies toward a `RetentionPolicyDays`
  policy (reusing U21's policy pattern). Raw storage paths are masked by role
  (`BackupCopyLocationMasker`) at the one read path every view and export shares, so a non-Admin/DBA
  role never sees the real path either way. **D18** — generically-named `MaintenanceEvidenceRecord`
  (never "DBCC result" — covers every engine) records integrity checks, replication/failover drills
  and required maintenance jobs with node/deployment (optionally reusing D07's `InstanceDependency`),
  tool/version, schedule, source (manual or a reviewed external monitoring/ticket link, which requires
  a reference), and outcome; pure `MaintenanceEvidenceStalenessClassifier` flags Missing/Failed/Stale
  distinctly from `BackupHealth` — nothing in this table is ever read by backup-health computation or
  vice versa, and a Passed result with no recorded schedule stays Unknown rather than being assumed
  current. **D19** — `CapacityMeasurement` (growth/free-space/workload/performance-baseline, each
  with source/unit/measurement-period) backs `MigrationCapacityEstimate` (assumptions required) and
  `WorkloadComparison` (optional links to a D10 campaign); pure `CapacityForecastBuilder` always
  returns its stated assumptions and missing-data gaps and projects an earliest/latest *plausible
  range* from the fastest/slowest observed growth intervals rather than ever asserting one precise
  exhaustion date. **D20** — `CertificateDependency` records TLS/recovery-key certificate expiry,
  ownership, a secret-store *reference* (Vault path, key name — never the secret), and a
  verified-backup/restore-access flag with its own last-check time; pure `CertificateExpiryEvaluator`
  and `KeyRotationEvaluator` are two entirely separate classifications — a certificate can be Ok on
  expiry while Overdue on rotation, or vice versa, by design. `CertificateDependencyService` screens
  every free-text field through a new `SecretLeakGuard` (PEM/private-key markers and
  `password=`/`secret=`-style assignments) before it's ever saved, and the Certificates page's CSV
  export re-screens the same way at export time — belt and suspenders, since no field on the entity
  is meant to hold a secret in the first place. A new disabled-in-Testing
  `CertificateExpiryCheckHostedService` reuses D03's `SupportRunwayNoticeEvaluator` threshold/dedupe
  logic (rather than a parallel implementation) to raise one deduplicated notice per crossed
  365/180/90/30-day threshold through the existing U07 subscription/notification plumbing. Three new
  per-instance pages — Recovery (D16-D17), Assurance (D18-D19) and Certificates (D20) — are linked
  from the instance detail page alongside the existing Lifecycle/Migration links. One new EF migration
  (`AddDbaRecoveryEpic21`) for the eight new tables plus `BackupRecord.ChainRole`; continuing the
  `NoAction`-on-shared-parent-table pattern from Epics 18-20, `MaintenanceEvidenceRecord.
  RelatedDependencyId`, `MigrationCapacityEstimate`'s `DatabaseInstanceId`/`PathAssessmentId`, and
  `WorkloadComparison`'s `DatabaseInstanceId`/`PreMeasurementId`/`PostMeasurementId` all use `NoAction`
  since `DatabaseInstance` already cascades to `InstanceDependency`/`PathAssessment`/
  `CapacityMeasurement` directly. 56 new unit tests (`BackupChainCoverageCalculatorTests`,
  `BackupCopyAvailabilityEvaluatorTests`, `RetentionCoverageComparerTests`,
  `BackupCopyLocationMaskerTests`, `MaintenanceEvidenceStalenessClassifierTests`,
  `CapacityForecastBuilderTests`, `CertificateExpiryEvaluatorTests`, `KeyRotationEvaluatorTests`,
  `SecretLeakGuardTests`) and 5 new E2E tests (`Epic21RecoveryAssuranceTests`) covering a missing
  chain member keeping coverage Incomplete rather than falsely green, a copy location moving between
  Accessible/CannotVerify, an integrity-check failure shown distinct from an unchanged backup-health
  badge, a capacity measurement whose forecast still states its assumptions/gaps with only one data
  point, and a certificate-expiry reminder plus a CSV export proving a planted secret-looking value
  never survives past redaction. Full suite: 598/598 (513 unit + 85 E2E). This completes Epic 21
  (D16-D20) in full; Epic 22 (D21-D22) remains proposed and unimplemented.
- 2026-11-10 — Built Epic 22 (D21-D22), the fifth and final epic of the DBA lifecycle roadmap — this
  closes the entire backlog: every originally-requested epic, the full usability roadmap (U01-U22,
  Epics 13-17) and the full DBA lifecycle roadmap (D01-D22, Epics 18-22) are now all complete. **D21**
  — a new `SupportContract` (vendor, coverage type/dates, owner, renewal-evidence reference) links to
  one or more instances via `SupportContractAssignment`; pure `EntitlementStatusClassifier` computes
  NoContractOnFile/Active/ExpiringSoon/Expired from contracts and "now" only — it deliberately never
  reads `VersionIdentity`/`ReleaseCatalogEntry`/`SupportRunwayDecision`, so a product's D02/D03
  lifecycle/support-phase status and its D21 entitlement status are always two independent facts that
  can legitimately disagree, shown side-by-side (with a direct link to the Lifecycle page) on a new
  per-instance `/instances/{id}/entitlement` page rather than derived from each other. Reviewed
  target-edition licensing constraints and an estimated extended-support-or-migration cost
  (`EditionCostEstimate`: currency, date, stated assumptions, reviewer) are commercial evidence,
  restricted to a new `Roles.CommercialEvidencePolicy` (Admin/ITManager — narrower than the usual
  DBA-inclusive `CanEditPolicy`, since a DBA can record a contract fact but not the dollar figures);
  pure `CostEstimateAggregator` sums reviewed estimates by type (withholding a total rather than
  summing mismatched currencies) and `CostEvidenceMasker` masks the figures for every other role, at
  the one page every viewer shares — same chokepoint pattern as D17's `BackupCopyLocationMasker`. A
  `CampaignAlternative.SourceCostEstimateId` link lets a D10 alternative trace back to the reviewed
  estimate it used, and a D21 estimate's id is citable directly in a D14 exception's existing
  evidence fields — both reuse what D10/D14 already have rather than adding a parallel mechanism.
  **D22** — a new `RetirementChecklist` per instance (optionally linked to a D10 campaign) is gated by
  pure `RetirementChecklistGateEvaluator`, which classifies seven gates — downstream consumers and
  jobs/replicas/linked-services/backup-tooling resolved (both reusing D07 `InstanceDependency`), final
  recoverable backup/key-access evidence retained (reusing D16's `BackupChainCoverageCalculator`, D17's
  `BackupCopyAvailabilityEvaluator`, and D20's `CertificateDependency.VerifiedBackupRestoreAccess`),
  the change's rollback window closed (reusing D13's `RollbackRetentionDeadlineUtc`), retention/
  legal-hold obligations reviewed (D17), application/platform sign-off, and cost/contract review
  (D21) — as Blocked, Unknown or NotApplicable, with Unknown and NotApplicable both deliberately
  *not* treated as passing: nothing here is ever silently promoted to green. Every gate satisfied, or
  a scoped, currently-Active D14 `ReadinessException` targeting that exact checklist (`TargetType`
  `"RetirementChecklist"`), is the only way `RetirementChecklistService.CompleteRetirementAsync` will
  proceed — and when it does, it calls nothing but the pre-existing U14 `InstanceArchiveService.
  ArchiveAsync`, preserving that service's own preview/reason/history behavior exactly, then stamps
  `RetiredAtUtc`/`RetiredBy` on the checklist for lineage. There is no second archive/delete path, no
  automated backup/data deletion anywhere in this epic, and no automatic legal/license compliance
  certification in D21 — both record-keeping/evidence layers only, exactly as scoped. One new EF
  migration (`AddSupportEntitlementAndRetirementEpic22`) for the four new tables plus
  `CampaignAlternative.SourceCostEstimateId`; continuing the `NoAction`-on-shared-parent-table pattern
  from Epics 18-21, `RetirementChecklist.DatabaseInstanceId` and `CampaignAlternative.
  SourceCostEstimateId` both use `NoAction` (the former for the same "also reachable via
  MigrationCampaign" shape as `ChangeValidation`/`RehearsalRecord`; the latter defensively, since
  nothing in this app deletes a cost estimate). `dotnet ef migrations has-pending-model-changes`
  reports clean, and the migration was verified by actually applying it to a live SQL Server database
  (not just generating it) — no cascade-path error. Found and fixed two real application bugs while
  building this: an EF Core query called `.Include(c => c.OwnerTeam)` *after* `.Select(a =>
  a.SupportContract!)` projected the query away from its root entity, which broke the entitlement
  page outright for every role (caught immediately by the first E2E test exercising that page, not
  guessed) — fixed by moving the `Include`/`ThenInclude` before materializing and projecting
  separately afterward. Second, the same recurring class of bug called out in the Epic 15-17 entries
  above: the cost-estimate amount field used a plain `@bind` (change/blur-triggered) instead of
  `@bind:event="oninput"`, so filling it and clicking "Record estimate" at realistic speed left the
  button stuck disabled — fixed the same way those earlier instances were. Also hardened a genuine
  (if minor) app bug, not just a test issue: `CostEvidenceMasker.MaskAmount` formatted figures with
  `{amount:N2}` against the server process's ambient thread culture, so the exact same dollar amount
  could render with different digit grouping depending on the host machine's locale — fixed to format
  with `CultureInfo.InvariantCulture` explicitly, so a cost estimate reads identically regardless of
  where the app happens to be running. 33 new unit tests (`EntitlementStatusClassifierTests`,
  `CostEstimateAggregatorTests`, `RetirementChecklistGateEvaluatorTests`) and 3 new E2E tests
  (`Epic22EntitlementAndRetirementTests`) covering a linked support contract whose Expired entitlement
  status is shown and reasoned about entirely separately from the instance's lifecycle/support-phase
  page, a cost estimate visible with its real figure to an ITManager but masked for a Viewer (who also
  has no path to record one at all), and a retirement checklist blocked on an unresolved downstream
  dependency, passing that one gate once resolved, still correctly blocked overall by the remaining
  unreviewed/unsigned gates, then bypassed via a scoped D14 exception and completed — verified by
  landing back on the instance detail page showing the pre-existing U14 "archived" banner and reason,
  and by confirming the instance actually appears in U14's own pre-existing archived-instances browser
  rather than some new mechanism. Full suite: 634/634 (546 unit + 88 E2E). This completes Epic 22
  (D21-D22) in full and closes out the entire backlog: Epics 13-22 (U01-U22, D01-D22) are now all
  complete, alongside the originally-requested Epics 1-12.
- 2026-11-11 — Thorough cross-epic verification pass after the full backlog (Epics 1-22) was marked
  complete: ran the full automated suite, then drove real cross-epic persona journeys (evidence →
  backdated restore → maintenance → compliance snapshot; Auditor/Viewer role composition; the D01-D22
  lifecycle chain) by hand against the live app rather than trusting each epic's own isolated tests.
  Found and fixed five real, previously-undetected bugs: (1) the `AddUsabilityEpics14And15` migration
  was missing an `AddColumn` for `RestoreEvents.DurationMinutes` — present in the entity/model snapshot
  but never actually added to the schema, so any restore-event insert failed with "Invalid column name"
  on a fresh database; fixed by adding the missing column to that historical migration (safe to amend —
  no deployed database depends on it yet). (2) `ArchivedInstances.razor` (U14) was gated
  `[Authorize(Roles = CanEditPolicy)]` at the page level, blocking Angela (Auditor) and Marcus
  (ITManager) from viewing archived-instance evidence/attribution at all, contradicting U14's own
  stated rationale; fixed to `[Authorize]` with the Unarchive button alone wrapped in an
  AuthorizeView, matching the gating pattern used everywhere else in the app. (3) `Exceptions.razor`
  (D14) had no role gating whatsoever on creating, revoking or renewing a risk-acceptance exception —
  any authenticated Viewer could create or revoke the exact scoped exceptions that bypass readiness/
  retirement gates across D08/D09/D11-D13/D22, while only Approve/Reject was actually protected
  (DecideAsync already validates PermittedApproverRole server-side); fixed by wrapping Create/Revoke/
  Renew in `AuthorizeView Roles="CanEditPolicy"`. (4) `CampaignChangeWindows.razor` was `[Authorize]`
  while every sibling campaign page (CampaignDetail, CampaignRehearsals, CampaignValidation) is
  `[Authorize(Roles = CanEditPolicy)]` — fixed to match. (5) `ChangeWindowDetail.razor`'s SetStatus,
  LinkException (attaching an emergency D14 exception) and AddJournalEntry had no role gating at all,
  unlike RecordApproval (already server-validated); fixed by wrapping those three in the same
  AuthorizeView pattern, leaving the page itself viewable by every authenticated role. Added a new
  `CrossEpicIntegrationTests.cs` (5 tests) covering all five fixes plus a composite U01/U04/U08/U10/
  U11/U12 evidence-health-maintenance-snapshot journey. Also hardened `TestServerFixture` to always
  dump the test server's own console output to a log file on dispose (previously only on a startup
  timeout) — this diagnostic is what actually surfaced bug (1) and a sixth finding below; it would
  otherwise have stayed invisible, since no existing test ever inspected the server's own log.
  **A sixth real bug was found but NOT shipped**: `Login.razor`'s `OnInitializedAsync` dereferences
  its `[CascadingParameter] HttpContext` unconditionally, which is null once the global
  InteractiveServer render mode re-runs that method on the live circuit — throwing an unhandled
  `NullReferenceException` on every single login (confirmed via the server log; invisible to every
  prior test because the browser happened to recover anyway). Fixing only that null check uncovered a
  second, deeper issue it had been masking: `SignInManager.PasswordSignInAsync`'s cookie write then
  reliably failed with "Headers are read-only, response has already started," because writing an auth
  cookie from inside an already-live Blazor Server circuit is fundamentally too late — the original
  HTTP response was already sent. A proper fix (a `MapIdentityLoginEndpoint` minimal-API POST mirroring
  the app's existing `MapIdentityLogoutEndpoint`, with Login.razor posting a plain HTML form to it) was
  verified correct at the HTTP level directly — valid `Set-Cookie` and a correct redirect, confirmed
  with curl against the real running app — but caused every Playwright-driven login to consistently
  land back on `/Account/Login?ReturnUrl=%2Fnot-found` instead, for a reason not root-caused in the
  time available (tried both with and without `data-enhance="false"` on the form; identical result
  either way). Rather than ship an unverified change to the one page every single user and test
  depends on, this was reverted to the original (crash-masked-but-functional) code. The bug — real,
  reproducible, and user-facing under the right timing, not just a test artifact — is left open for a
  dedicated follow-up with time to validate the fix against the whole E2E suite, not just one test.
  **Live E2E confirmation was incomplete**: after this verification pass's many consecutive full-suite
  runs on top of the same session that built Epics 13-22, later runs showed generic page-navigation
  timeouts (e.g. plain `networkidle` waits on ordinary pages, well after a successful login) that
  reproduced identically on previously-100%-reliable, untouched test files — consistent with this
  single dev machine's resources being exhausted after many hours of continuous dotnet/SQL Server/
  browser activity, not a code defect. Confidence in the shipped fixes rests on: 546/546 unit tests
  (unaffected by this, and always green), an earlier clean 87/88 E2E run (the 1 failure confirmed
  passing in isolation) establishing the migration fix, an earlier clean 3/3 run of the new
  CrossEpicIntegrationTests file establishing the ArchivedInstances fix, and the Exceptions/
  ChangeWindowDetail/CampaignChangeWindows fixes being reviewed directly against the exact
  AuthorizeView pattern already proven correct on dozens of other pages in this app. Recommend a full
  E2E suite rerun in a fresh session/machine state as a final confirmation.
- 2026-11-11 — Resolved the deferred Login.razor bug above, and found that the "dev-machine resource
  exhaustion" explanation for the recurring E2E navigation timeouts was wrong. **Login fix:** the root
  cause was `<Routes @rendermode="InteractiveServer" />` in App.razor applying the interactive circuit
  to every page, Login included. A static root can host interactive descendants but not the reverse,
  so Login couldn't opt out on its own. App.razor (always static SSR, so its own `HttpContext` is the
  real per-request one) now picks the render mode per request: `null` (static SSR) for
  `/Account/Login`, `InteractiveServer` for everything else, so the rest of the app keeps its single
  persistent circuit (the toast-bus fix). A first version carved out all of `/Account`, which broke
  ForgotPassword (caught by its existing E2E test) and, silently, ResetPassword and
  `/account/change-password` (no E2E coverage): those pages use `EditForm OnValidSubmit` without a
  `FormName`, so they only work interactively. The carve-out was narrowed to Login, the only page that
  writes the auth cookie. A new regression test,
  `TemporaryPassword_ForcesChangePassword_AndTheNewPasswordSignsIn`, then surfaced a separate
  pre-existing U16 bug: users created with a temporary password were
  never actually forced to change it. On .NET 10, `NavigationManager.NavigateTo` no longer throws during
  static SSR, so Login's `RedirectTo(ReturnUrl ?? "/")` right after the change-password redirect won.
  Fixed with an explicit `return`, and the test now passes. **Wait-strategy fix:** with the
  render-mode fix applied, a headed run of the 5 CrossEpicIntegrationTests still failed 5/5, every one
  a 30s timeout in `GotoAsync(..., WaitUntil = NetworkIdle)` on an authenticated page, while the server
  log showed zero exceptions. `networkidle` needs about 500ms with no network activity, which a page with
  a persistent SignalR circuit plus background polling (U03 auto-refresh, notification bell) can't
  reliably provide. All 30 `WaitUntilState.NetworkIdle` uses across 23 E2E files were changed to
  `WaitUntilState.Load`. Every call site already follows navigation with its own
  `WaitForSelectorAsync`/locator assertion, so the "page actually rendered" check is unchanged.
  Headed runs are now opt-in via `E2E_HEADED=1` (TestServerFixture). **Verified:** one complete run,
  546/546 unit and 94/94 E2E (93 existing plus the new test), with zero exceptions in the shared test
  server's log for that run (Testing logs at Warning and above; the earlier failing run logged 196
  `NullReferenceException`s through the same capture). **Inferred, not proven:** that `networkidle`
  rather than machine load caused the earlier passes' late-session timeouts. The symptom matches
  exactly, the server log was clean, and switching waits removed it across a full run, but machine
  load wasn't independently measured. **Not addressed:** each test file still has its own copy of
  the `GotoAsync` and fixed 800ms `WaitForCircuitAsync` helpers (no shared base class). Consolidating
  them would stop this recurring but was left out to keep the change small.
- 2026-11-11 · Antislop cleanup pass (mode: during), run as parallel agents on separate git
  worktrees and merged into main. The owner delegated the pattern calls, so the terminal-window login
  card, grid-paper background, monospace headings/nav, `//` section labels and blinking wordmark
  cursor were all dropped (Design section above has the current look). **UI and layout:** plain
  sign-in panels; three colour tokens raised to WCAG AA; visible focus outlines (Bootstrap's
  `outline: 0` had been hiding them on fields); proper `aside`/`nav`/`main` landmarks; a page
  `ErrorBoundary` in MainLayout so one failing page no longer kills the circuit; 360px phone pass
  (wide `dbt-table` tables now scroll inside their container instead of widening the page,
  instance-detail action buttons wrap, dashboard count cells form a 2x2 grid). **Pages (35 files):**
  em dashes and internal backlog IDs (U21, D22, "Epic 20") removed from visible text; empty cells say
  what they mean ("Never", "Unassigned", "No successful backup yet"); buttons name their action;
  every input labelled (`for`/`id`), every table named; load-failure states with "Try again" on
  instance pages; notification items are real buttons. **Code:** 1,744 comment lines removed from
  236 C# files (restatements and epic/persona references); comments explaining a non-obvious reason
  were kept. About 70 user-facing messages in Core/Infrastructure (archive previews, gate errors,
  notifications, report email subjects) rewritten without em dashes. **Bugs found along the way:** a
  failed lifecycle manual override showed a success toast; the sidebar clipped the notification
  dropdown; the bell's refresh button dropped keyboard focus so Escape stopped closing it.
  **Verified:** full headed run (`E2E_HEADED=1`), 546/546 unit and 94/94 E2E, zero exceptions in the
  test server log. The first headed run after merging was 87/94: five tests pinned old wording or
  markup that the pass had changed (restored where the test checks real behavior; the bell test now
  targets the corrected `role="region"` instead of the old wrong `role="menu"`), one Reports test
  counted the summary strip before the new loading state finished (now waits for it), and one was a
  missing space in a heading introduced during the merge. **Not addressed:** a light-theme toggle and
  the remaining em dashes inside code comments (not user-facing).
- 2026-11-11 · Built I01, the first-run setup wizard at `/install`. In Production (Development and
  Testing are unchanged), an app with no `App_Data/dbtracker.config.json` and no working pre-wizard
  deployment sends every page to `/install`, skips migrations, and keeps all six background workers
  waiting on `InstallState.WaitUntilInstalledAsync`. Steps: one-time setup key (written to
  `App_Data/setup-key.txt` and the log, constant-time compare, 5 wrong tries per minute per client),
  SQL Server connection with Windows or SQL login (Test connection, create the database with a
  `QUOTENAME`'d parameter, or keep an existing DBTracker database and apply only pending migrations),
  table creation shown migration by migration, optional sample data plus the five demo accounts behind a
  "not for production" warning, a required first Admin, optional email (ArxMail first, SMTP, or none) with
  a test send that reports the real provider outcome, an optional backup-share read check as the running
  identity, then a summary. Finishing creates the Admin before writing the config (so a failure can't
  leave an installed app nobody can sign in to), writes the file with SQL, SMTP and ArxMail secrets
  encrypted by Data Protection (keys in `App_Data/keys`, DPAPI machine scope on Windows), records an
  audit event, deletes the setup key and lands on the login page. **No restart needed:** the config file
  is loaded through a decrypting JSON provider and reloaded on finish, a singleton
  `InstallAwareDbContextFactory` stamps each new context with the current connection string, and the
  email sender is resolved per request. After setup `/install` answers 404, including after a restart.
  The dev connection string moved from appsettings.json to appsettings.Development.json, since otherwise a
  fresh Production deploy would inherit `Server=localhost` and never show the wizard (the deployment had
  no other way in: Production never seeded any user). Static web assets now load in every environment
  (a no-op when published), so a Production run from the build output serves its CSS and scripts. **Real
  bugs found:** a first version built DbContext options per scope; contexts created by the notification
  bell's timer after its page closed then hit a disposed service provider and the unhandled exception
  killed the server (56 of 94 existing E2E tests failed with connection refused). Replaced with the
  singleton factory above. The app's own not-found page requires sign-in, so a re-executed 404 for
  `/install` turned into a login redirect; the gate now writes its own 404. At 360px, a phone-only rule
  from the antislop pass set `min-height: 44px` on `.form-check-input`, stretching every checkbox and
  radio in the app into a tall bar; the row (label plus input) is now the 44px target instead. Browser
  validation on `type=email` fields blocked the wizard's own error list, so its forms are `novalidate`.
  **Verified:** 592/592 unit (46 new) and 99/99 E2E (94 existing plus 5 new install tests: redirect and
  wrong key, full wizard to the Admin's dashboard, 404 after setup, settings read back after a restart,
  sample data with a demo login), each install test on its own port, data folder and throwaway
  `DBTracker_InstallTest_*` database, dropped on teardown. Screenshots at 1280px and 360px checked: no
  horizontal overflow, visible focus, no console errors from the app (the only 404 is the
  `hot-reload-settings.json` dev-tooling request seen in any unpublished run).
- 2026-11-11 · First real install of `DBTracker-Setup.exe` (I03/I04), in a clean Windows Server 2025
  evaluation VM on Hyper-V. Windows Sandbox was tried first and cannot host IIS: its image has no IIS
  features, so DISM rejects every name (0x800f0831) and Setup stopped cleanly before installing
  anything; the setup helper now logs that plainly and DEPLOYMENT.md lists it under troubleshooting.
  `installer\test\Start-HyperVTest.ps1` builds the VM from a differencing disk (the downloaded VHD is
  never modified), turns on automatic sign-in, runs Setup on the VM desktop with its progress window
  visible, and streams the logs back. **First VM run:** IIS with WebSockets, the .NET 10.0.15 Hosting
  Bundle and SQL Server 2025 Express all installed (the Express silent arguments, previously taken
  from third-party guides, are now verified), but `DBTracker.msi` failed with 1603. **Real bug:** the
  managed custom actions threw BadImageFormatException because DTF picks the native SfxCA host from
  `$(Platform)`, not `PlatformTarget`, and fell back to x86, wrapping the x64 assembly in an x86 host;
  the custom-action project now pins the x64 SfxCA. Earlier verification had only called that code
  directly from PowerShell, never through Windows Installer, which is why it was missed. **Second VM
  run:** Setup exit 0, MSI "Installation completed successfully", site Started, app pool No Managed
  Code, `/install` returned HTTP 200, setup key file created, Edge opened the wizard. **Not yet
  covered:** clicking through the bundle's Options page (the VM runs used `/passive`), uninstall, and
  an upgrade from one version to the next.
- 2026-11-11 · Uninstall tested in the same VM with `installer\test\Test-HyperVUninstall.ps1`
  (install with a SQL login, normal uninstall, reinstall plus `/uninstall RemoveData=1`, final
  install). Three real bugs found and fixed: (1) the SQL login was never created, because
  `EXEC(N'CREATE LOGIN ' + QUOTENAME(@login) ...)` is invalid T-SQL (EXEC takes only literals and
  variables), reproduced with SET PARSEONLY and fixed by building the statement in a variable and
  running it with sp_executesql; because a failed grant only warns, every install had "succeeded"
  without it; (2) Setup.exe had no way to pass REMOVE_DATA, and the msiexec route left a stale "DBTracker
  Setup" entry in Settings > Apps; the bundle now has a `RemoveData` variable; (3) deleting App_Data left
  an empty install folder, because Windows Installer had already tried to remove it; the custom action
  now removes it when empty. **Verified:** all six runs exit 0; normal uninstall removes site, pool,
  port binding, program files and the Apps entries but keeps App_Data and the prerequisites;
  `RemoveData=1` also removes App_Data and the folder; the login `IIS APPPOOL\DBTracker` with dbcreator
  is created. The SQL login is deliberately kept on uninstall (a database may still map a user to it);
  DEPLOYMENT.md gives the DROP LOGIN line. **Still not covered:** the Options page click-through and a
  version-to-version upgrade.
- 2026-11-11 · Built I06, personal access tokens and the tray API. `/account/tokens` (any signed-in
  role, linked from the user menu) creates a named token with a 30/90/180/365-day expiry (default 90),
  shows it once with a Copy button, lists created / last used / expires / status, and revokes. Tokens
  are `dbt_` plus base64url of 32 random bytes; only a SHA-256 hash and a 10-character display prefix
  are stored, checked in constant time, and last used is written at most once a minute
  (`PersonalAccessTokenCodec`, pure and unit-tested). Create and revoke go through the U19 audit
  recorder, and deactivating a user revokes their tokens as well as failing them on every request via
  the active-account check. `GET /api/tray/summary` uses a separate bearer-only authentication scheme
  applied by policy to that endpoint alone, so cookie sign-in and the Login render-mode fix are
  untouched; it never redirects (the handler turns off status-code pages, which would otherwise
  re-execute a 401 into the sign-in-only not-found page and so into a login redirect) and returns the
  dashboard's counts and its worst-first at-risk list (max 20, `TraySummaryBuilder`). Anonymous
  `GET /healthz` reports status, setup state, database reachability (3-second probe, cached 10
  seconds so many tray apps cost one connection) and version, with no other detail; the install gate
  lets it through before setup and answers `/api/*` with a 503 JSON instead of the wizard redirect. One
  EF migration (`AddPersonalAccessTokens`). No product bugs found; two test-only mistakes fixed along
  the way (a bare `button[type=submit]` on the change-password page clicked the sidebar's Log out
  button, and an unnecessary wait on Blazor's in-page navigation was timing-dependent). **Verified:**
  619/619 unit (27 new) and 103/103 E2E (4 new `AccessTokenTests`, plus before/after-setup `/healthz`
  and `/api` checks added to the Production install tests).

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

## Usability roadmap — 2026-11-10 review

Source and rationale: [project usability review](USABILITY_REVIEW.md), including implementation
evidence, persona challenges, scope decisions, dependencies, and proposed user-task validation.
All items below are **proposed and unimplemented**. Persona agreement is a structured assessment,
not user interviews or a claim of stakeholder approval. Existing Epics 1–12 remain delivery history.

Priority: **P1** = next delivery candidates (trust/task blockers); **P2** = subsequent usability
improvements; **P3** = later enhancement. Effort **S/M/L** is relative, not a time commitment.
The U identifiers are canonical: references from existing epics describe the same work.

### Epic 13 — Explainable health and trustworthy evidence

- [x] **U01 · P1 · L — Distinguish discovered files from verified backups.**
  Priya/Angela agree that file presence alone must not imply a valid full backup. Dev wants one
  understandable summary with evidence details available on demand.
  **Acceptance:** configure per-instance filename/type rules with preview; exclude unrelated,
  zero-length and still-changing files; distinguish discovered, reported-success and restore-tested
  evidence. Surface filename, size, observed time and verification level. Detect changed files with
  reused names without duplicating unchanged evidence; concurrent scans do not duplicate it.
  Existing discovered records retain provenance and are not silently upgraded to verified status.

- [x] **U02 · P1 · M — Detect stale scanners and explain recovery.**
  Priya/Noor need to know when monitoring has stopped; Angela wants scan health kept separate from
  backup health.
  **Acceptance:** an old successful scan becomes Stale after a configurable threshold tied to the
  scan interval; show last attempt, last success, next expected check, disabled status and a useful
  recovery hint. Dashboard links directly to affected Scan Activity rows. Scanner recovery clears
  the warning; a stale scanner never turns an otherwise failed backup green. Depends on U05 for
  shareable report/filter URLs, but age-based detection can ship first.

- [x] **U03 · P1 · M — Refresh operational views with visible data age.**
  Priya/Noor need current information while a page stays open; Alex requires stable focus.
  **Acceptance:** dashboard, inventory and notification counts show last-updated time plus manual
  refresh; periodic refresh can be paused. Health changes when a deadline passes and when another
  session logs evidence. Refresh preserves filters/focus and does not reset in-progress forms;
  reconnect/error state is visible. Use isolated reads to avoid shared-context query races.

- [x] **U04 · P1 · M — Explain each health result and the next deadline.**
  Priya wants the reason and next action; Dev wants plain language; Angela wants traceable inputs.
  **Acceptance:** show last success, latest attempt/failure, governing interval, next due time and
  overdue duration. Separate backup status, scan freshness and maintenance suppression. Ignore
  future-dated evidence when evaluating an earlier instant; define timestamp tie handling. Manual,
  discovered and verified evidence labels stay consistent with U01 across UI, alerts and reports.

### Epic 14 — Faster navigation and triage

- [x] **U05 · P2 · M — Saved, shareable views and consistent filtering.**
  Priya/Marcus want repeatable production/team views; Dev wants My instances as a saved default.
  **Acceptance:** persist filters, sort, report type and date window in validated URLs; add dedicated
  owner/department/tag facets and column sort controls. Back navigation restores the prior list.
  Dashboard counts and drill-down use the same scope; Scan Activity can open directly. Saved views
  can be renamed/reset and respect each viewer's permissions; personal filters grant no access.

- [x] **U06 · P2 · L — Bounded inventory and history browsing.**
  Priya/Marcus want usable large inventories; Alex wants predictable controls over endless rows.
  **Acceptance:** query-side filtering/sorting/paging with counts and stable ordering; overview
  queries fetch required summaries rather than all histories. Backup/restore/change history is
  paged and filterable. Export covers the entire selected result, with explicit scope. Benchmark
  the representative dataset in the review and agree response-time targets from that baseline.

- [x] **U07 · P1 · M — Personal notification inbox and preferences.**
  Extends Epic 8's existing open preference item. Dev wants relevant notifications; Noor needs
  direct navigation; Angela requires reading to remain separate from operational acknowledgement.
  **Acceptance:** per-user read state, single/all-read actions, instance links and paged history;
  one user's read action never clears another's inbox. Subscribe by instance/company/ownership and
  channel with understandable defaults. Define migration of existing shared notifications/read
  flags explicitly; subscriptions always respect authorization. Shared incident state belongs to U08.

- [x] **U08 · P2 · L — Acknowledge, assign and hand off outstanding risk.**
  Priya/Noor need to avoid duplicate response; Marcus wants accountable owners; Angela rejects
  acknowledgement as evidence of recovery.
  **Acceptance:** track assignee, acknowledgement, note/runbook/ticket reference and activity log;
  acknowledgement leaves health unchanged. Configure reminders/escalation and NeverBackedUp
  alerts after an onboarding grace period; recovery closes the incident and can notify subscribers.
  Define re-alerting after maintenance, including instances already alerted before maintenance.
  Quiet hours/maintenance must expose suppression and escalation behavior. Depends on U07/U19.

### Epic 15 — Reporting that matches user intent

- [x] **U09 · P1 · M — Make report scope and delivery outcomes explicit.**
  Marcus/Angela need to know what was sent to whom; Sam needs useful delivery diagnostics.
  **Acceptance:** preview filters, generation time and permitted recipients before sending; make
  current filtered scope versus full inventory explicit and send exactly the confirmed scope.
  Download/preview/send share one dataset. Show disabled, queued/accepted, failed and partial
  outcomes accurately: a no-op sender cannot produce a sent-success message. Never equate provider
  acceptance with recipient delivery; retain attempt status for diagnosis. Builds on U15's diagnostics.

- [x] **U10 · P2 · L — Reproducible compliance snapshots and custom report dates.**
  Angela wants repeatable evidence; Marcus wants review-period exports; Priya wants incident windows.
  **Acceptance:** custom from/to ranges with explicit timezone and boundary rules; store generated
  snapshots with ID, actor, scope, generation/effective time, policy version and evidence provenance.
  Reopening/exporting a snapshot preserves its values after later edits, archives or backdated entries.
  Initial release supports retained snapshots, not arbitrary past inventory reconstruction. A future
  historical-as-of view needs versioned inventory/policy and excludes later backup/restore events.
  Depends on U01/U04/U11/U19. Promotes the former custom-date future item into this roadmap.

- [x] **U11 · P2 · M — Record actual event time and inspect complete evidence.**
  Priya needs to log a backup after an incident; Angela needs event time distinguished from entry.
  **Acceptance:** backup/restore entry defaults to now but allows an explicit past occurrence time
  with timezone; retain entry time and authenticated recorder separately from performed-by. Validate
  values and future times. History reveals notes, source file and evidence references; reasoned
  append-only corrections preserve the original. Common entry remains short; use U13 validation
  and U19 audit conventions. Backdating never silently changes a retained U10 snapshot.

- [x] **U12 · P2 · M — Restore-test recency and evidence linkage.**
  Angela wants recent proof; Marcus wants an actionable drill list; Priya wants realistic policies.
  **Acceptance:** configurable test interval by policy/instance, last success, next due date and
  Never tested/Current/Overdue states. Link tests to backup evidence when known; retain tester,
  evidence reference and optional duration/target recovery time. Missing linkage stays explicit,
  rather than invalidating old records or claiming every backup was tested. Report expired coverage
  separately from backup health. Depends on U11; policy templates can follow under U21.

### Epic 16 — Safer entry, recovery and administration

- [x] **U13 · P1 · M — Prevent lost edits and recover from failed saves.**
  Priya/Dev need safe editing; Sam wants errors users can correct; Alex needs accessible feedback.
  **Acceptance:** shared form/import validation for required fields, storage lengths, positive
  intervals/retention and nonnegative measurements; validate supported link schemes and enum values.
  Associate field errors with controls and show a summary. Guard dirty navigation, disable duplicate
  submits and preserve inputs after failures. Detect concurrent edits and offer reload/review without
  overwriting newer changes. Missing instance IDs produce a useful not-found state, not endless load.

- [x] **U14 · P1 · M — Archive preview, archive browser and unarchive.**
  Sam/Priya need recovery from accidental archiving; Angela needs preserved evidence and attribution.
  **Acceptance:** preview effects on scans, alerts and current reports; confirm with a reason.
  Authorized users can browse/search archived records and restore them. Preserve history; record
  actor/reason/time; exclude archived instances from normal sweeps and indicate them clearly on
  direct links. Recompute health and alert eligibility on unarchive. Uses U19 audit conventions.

- [x] **U15 · P2 · M — Guided setup and monitoring diagnostics.**
  Sam wants to diagnose setup without database/log access; Dev wants actionable empty states.
  **Acceptance:** Admin setup checklist for instance import/create, file-path access under the
  service identity, scan/alert/report enablement and distribution list. Show last/next worker run,
  configuration problems and send-attempt outcomes. Explicitly triggered test delivery reports
  accepted/failed/disabled accurately. Mask credentials and restrict sensitive paths/details to
  appropriate roles. Empty states link to a permitted next action. Use existing mail integration.

- [x] **U16 · P1 · M — Complete account lifecycle and password recovery.**
  Refines Epic 5's open item. Sam needs offboarding without database edits; Dev needs recovery from
  a forgotten or temporary password; Angela needs an accountable role-change history.
  **Acceptance:** change roles, deactivate/reactivate, change own password and recover access via
  expiring single-use reset/invitation links using the configured mail integration. Require changing
  a temporary credential; prevent removing the last active Admin. Define active-session and pending
  device-link revocation on deactivation/role downgrade; audit changes. Neutral reset responses and
  actionable disabled-mail guidance avoid misleading confirmations. Enterprise SSO remains deferred.

- [x] **U17 · P2 · L — Safe repeatable imports and reviewed bulk edits.**
  Priya/Sam want to onboard and maintain many records; Angela requires visible, attributable changes.
  **Acceptance:** choose an explicit stable identity/matching rule; preview create/update/skip and
  ambiguous duplicates within a file and existing inventory. Repeating an import does not duplicate
  instances. Support quoted commas/newlines, malformed-row errors and downloadable corrections.
  Validate like U13, handle file limits gracefully and define transaction/partial-success behavior.
  Bulk owner/tag/policy changes show selected count and before/after diff; recheck permission and
  conflicts at apply time and audit the batch. Archive remains a separate consequential action.

- [x] **U18 · P2 · S — Configurable connectivity endpoints.**
  Priya needs valid checks for nondefault ports; Dev needs an understandable result.
  **Acceptance:** optional host/DNS and validated port override with engine defaults; show actual
  host:port, check time, timeout and actionable failure. Disable checks when no endpoint is set.
  State that TCP reachability does not prove database login, backup success or recoverability.

- [x] **U19 · P2 · L — Extend audit coverage across operational changes.**
  Angela/Sam want consistent accountability; Priya needs event corrections without erased history.
  **Acceptance:** record actor, time, reason, target and before/after where relevant for creation,
  edits, archive/unarchive, maintenance, imports/bulk edits, account changes, incident actions and
  manual evidence/corrections. Distinguish system actors from people. Append-only application
  workflow, authorized searchable/exportable history and documented retention. Do not describe an
  application audit table as tamper-proof against database administrators. Define the shared audit
  schema early so U08/U11/U14/U16/U17 can use it without waiting for the audit-browser UI.

### Epic 17 — Inclusive workflows and organizational context

- [x] **U20 · P1 · M — Accessible mobile workflows within the current theme.**
  Alex/Noor need equivalent task access; Priya wants the familiar ops console preserved.
  **Acceptance:** semantic instance/report links, named form/filter inputs, keyboard sorting and
  visible focus; dropdown Escape/focus handling; announced save/error/refresh feedback. At 360px
  and 200% zoom, primary actions remain reachable and tables scroll within labelled regions or
  offer concise stacked rows. Check text/status contrast and touch controls. Preserve existing
  reduced-motion support and status text. Verify core tasks by keyboard, screen reader and mobile
  viewport; no theme replacement is required.

- [x] **U21 · P2 · L — Team ownership, structured facets and reusable policies.**
  Refines Epic 7's open environment-tag item. Marcus needs ownership beyond one email; Dev wants
  a useful personal view and a safe way to correct contacts; Priya wants fewer repeated settings.
  **Acceptance:** owner team, primary/backup contacts and explicit user/team memberships power
  My instances and subscriptions; support region/cluster/replica-set facets alongside existing tags.
  Version reusable cadence/retention/restore-test policies with per-instance overrides and an impact
  preview. Show missing/unconfirmed ownership and allow contact correction requests with authorized
  review. Preserve existing contact data on migration. Personalization does not establish row-level
  security; organization isolation requires a separately designed authorization feature.

- [x] **U22 · P3 · L — Trends and review-ready risk summaries.**
  Marcus wants to see whether risk improves; Priya wants repeated failures and duration growth;
  Angela wants clear denominators and evidence limitations.
  **Acceptance:** compare periods for health coverage, outstanding risk age, repeated failures,
  backup duration/size and restore-test recency; drill into scoped supporting rows. Include inventory
  denominator, missing evidence and policy changes. Use U10 snapshots/U19 events for historical
  status rather than inferring past health from today's inventory. Accessible tables accompany any
  charts; retention values alone do not claim that files remain available on disk.

### Proposed sequencing and completion standard

Start with U01-U04, U07, U09, U13-U14 and U20; prioritize U16 alongside these for account lifecycle
needs. Then deliver saved views, scalable browsing, incident handling, event entry, restore recency,
setup, imports and endpoints. Define U19's common audit schema early; develop snapshots and
organizational policies before historical trend claims. Dependencies above describe integration
requirements, not permission to leave earlier features with misleading status.

For each implemented item: complete its acceptance criteria, verify relevant role boundaries,
add focused regression tests for changed behavior and run the associated persona task from
[USABILITY_REVIEW.md](USABILITY_REVIEW.md#recommended-delivery-and-validation). Establish task-time
and performance baselines before claiming improvements. These planning checkboxes remain open
until implementation and verification are complete.

Review log: 2026-11-10 — Reviewed current code and test sources from seven persona perspectives;
added 22 prioritized work items, acceptance criteria and sequencing, refined existing unfinished
items, and corrected sorting/shared-filter/scan-freshness claims. Documentation only; no features
were implemented or reclassified as shipped by this review.

## Backlog update history - 2026-11-10

Tracking convention: preserve existing backlog entries and checkbox states. Record every new
feature or change as a separate dated entry with a unique reference. For changes, identify the
original epic/item or U identifier, describe the new scope/status, and state whether the entry
clarifies, extends, or supersedes the earlier entry. Earlier wording remains historical context.

- 2026-11-10 | UPDATE-001 | Clarifies Epic 1, filterable/sortable grid: current inventory is name-sorted; interactive column sorting and dedicated department/owner facets are proposed in U05. The original completed entry is preserved.
- 2026-11-10 | UPDATE-002 | Clarifies Epic 3, shared combined filter bar: dashboard and inventory currently have separate, unequal controls; consistent shared filtering/state is proposed in U05. The original completed entry is preserved.
- 2026-11-10 | UPDATE-003 | Extends Epic 5, deactivate/change user role: U16 adds acceptance criteria and related account recovery/session lifecycle work. The original open item remains open; U16 links the refinement rather than representing a second implementation of role management.
- 2026-11-10 | UPDATE-004 | Extends Epic 7, custom environment tags: U21 specifies structured facets and broader ownership/policy improvements. Free-text Tags already exist; structured facets remain proposed.
- 2026-11-10 | UPDATE-005 | Extends Epic 8, notification preferences: U07 specifies subscriptions and personal read state. The original preference item remains open.
- 2026-11-10 | UPDATE-006 | Extends Design, further polish: U20 specifies functional mobile/accessibility acceptance criteria. Light mode and illustrations remain optional polish; the original item is preserved.
- 2026-11-10 | UPDATE-007 | Clarifies Epic 12, scan-staleness indicator: delivered behavior covers Error/NeverScanned, while elapsed-time staleness is proposed in U02 and direct report/filter navigation in U05. The original completed entry is preserved.
- 2026-11-10 | UPDATE-008 | Supersedes the prioritization of the future custom report date-range item: it is now a proposed P2 item within U10. The original future entry is retained as history; implementation remains incomplete.
- 2026-11-10 | UPDATE-009 | Tracking correction: the review log above describes corrections/refinements from the usability review. Their authoritative clarification is now recorded in these separate update entries; original backlog wording has been restored and preserved.
- 2026-11-10 | NEW-001 | Added U01-U22 under Epics 13-17 as separate proposed features/refinements, with priorities, effort, persona rationale and acceptance criteria. These remain unimplemented; see USABILITY_REVIEW.md for supporting evidence.
- 2026-11-11 | UPDATE-015 | Supersedes the Design section's "dark ops-console theme" and "Motion" entries: the antislop pass replaced the terminal login card, grid-paper background, monospace headings and `//` labels. The old entries are kept struck through; the new "Current (2026-11-11)" entry and the dated Progress log entry describe what shipped. The open "Further polish" item stays open (no light-theme toggle).
- 2026-11-11 | UPDATE-016 | Closes two original open items now delivered by their roadmap refinements (UPDATE-003, UPDATE-004): Epic 5 "deactivate / change role" by U16 (Users page role select plus Deactivate/Reactivate, covered by Epic16AccountLifecycleTests) and Epic 7 "region, cluster, replica set" by U21 (instance editor fields and facets, covered by Epic17OwnershipTests). Verified in the code before closing.
- 2026-11-10 | UPDATE-015 | Epics 13 and 14 (U01-U08) are now implemented — see the Progress log entries dated 2026-11-10 for what shipped and any scope notes. This supersedes UPDATE-005 (U07 delivered, so Epic 8's preference item above is now marked done) and partially supersedes UPDATE-001/UPDATE-002/UPDATE-007 (U05's facets/shared filtering/sort and U02's staleness detection are delivered; U05's report/filter deep-linking and U06's full remaining scope are covered in the Epic 14 entry). U03/U04's items are also delivered per Epic 13. Earlier wording is preserved as history; U09-U22 remain open.

## DBA lifecycle roadmap - 2026-11-10

2026-11-10 | NEW-002 | Added this separate DBA-focused roadmap after reviewing database release
availability, support lifecycle, upgrades/migrations and operational readiness. Existing backlog
lines and checkbox states remain unchanged. Detailed evidence, official vendor references, persona
challenges and validation scenarios: [DBA_LIFECYCLE_REVIEW.md](DBA_LIFECYCLE_REVIEW.md).

All D items below are proposed and unimplemented. Each has its own dated tracking line. P1/P2/P3
and S/M/L use the earlier relative priority/effort definitions; priorities are not assertions of
actual fleet risk. Persona assessments represent structured design review, not stakeholder approval.
Release availability, lifecycle risk, security exposure and backup health must remain distinct.
A newer release alone does not make migration required. No automatic upgrade execution is proposed.

### Epic 18 - Database identity, releases and support lifecycle

- [x] **2026-11-10 | D01 | NEW | P1 | M - Structured installed-version identity.**
  DBA/platform/security personas need an exact product match before evaluating support or patches.
  **Acceptance:** retain raw Version text while adding normalized family/major/minor/build,
  edition/distribution, patch train and hosting/provider context. Record observation source/time,
  attestation and unresolved mappings; unknown/unrecognized data cannot appear compliant. Separate
  hosting engine, database compatibility state and cluster-node versions. Preserve IDs and current
  imports; changing a text field alone never proves an upgrade occurred. Foundation for D02-D09.

- [x] **2026-11-10 | D02 | NEW | P1 | L - Reviewed release and support catalogue.**
  DBA/auditor/platform personas need current, attributable facts, including offline installations.
  **Acceptance:** curate/import official product releases, GA/preview/channel, patch/build branches,
  support phases and milestones with source references, applicability, date precision/timezone,
  retrieved/reviewed time and revision history. Context includes edition/distribution/provider where
  relevant. Distinguish vendor facts from internal policy and contractual coverage. Support manual
  override with reason, review and expiry. Conflicts/TBD dates stay explicit; refresh failure retains
  last-known facts with a staleness warning. No fixed universal EOL formula. Depends on D01.

- [x] **2026-11-10 | D03 | NEW | P1 | M - Support runway and deadline notifications.**
  DBA/security/service-owner personas need enough lead time to act; auditor rejects implied paid
  coverage or suppressed risk.
  **Acceptance:** show standard support phases, applicable extended/ESU coverage evidence, days to
  next relevant milestone and Unknown/Needs review separately. Configurable 365/180/90/30-day
  notices route to owners, deduplicate and escalate; overdue work remains visible. Date revisions
  and contract expiry trigger reevaluation. Planned maintenance does not erase lifecycle risk.
  Link each decision to upgrade/patch/replace/retire/exception action. Depends on D01-D02; uses U07/U08.

- [x] **2026-11-10 | D04 | NEW | P1 | M - Release availability and approved patch baselines.**
  DBA wants relevant patch options; application owner wants certified targets instead of forced
  adoption of the newest version.
  **Acceptance:** display installed, latest applicable branch patch, newest relevant GA and approved
  organizational target separately, with release notes and comparison rationale. Respect CU/GDR,
  LTS/Innovation, distribution builds and provider availability; exclude previews by default. Show
  Available/Applicable/Approved/Required-by as separate facts. Unsupported/unreviewed targets are
  not recommended automatically; a patch within a supported branch is distinct from major migration.
  Depends on D01-D02; policy ownership integrates with U21.

- [x] **2026-11-10 | D05 | NEW | P2 | L - Advisory applicability and patch remediation.**
  Security/DBA personas need exposure assessment; auditor needs uncertainty and exceptions visible.
  **Acceptance:** ingest/reference official advisories with vendor IDs, affected builds/configurations,
  fixed builds and provenance. Classify Potential/Confirmed/Not applicable/Unknown with reviewer
  evidence; respect distribution backports and managed-service responsibilities. Prioritize using
  severity, service criticality and exposure; version matching alone cannot confirm vulnerability.
  Track owner, remediation deadline, target patch and verification; use D14 for temporary exceptions.
  Support imported results and shared vulnerability-service references without duplicating that platform.

- [x] **2026-11-10 | D06 | NEW | P2 | L - Version attestation and optional metadata collection.**
  DBA/platform personas want trustworthy inventory without mandatory credentials in the tracker.
  **Acceptance:** manual attestation and documented metadata import work first; optional read-only
  engine/provider collectors are explicitly configured later. Record actual build/edition/node and
  compatibility state, source/time, permission failures and drift against declared values. No failed
  connection overwrites last-good evidence or marks an asset retired. Use least-privilege collection,
  approved secret references and bounded schedules; never request broad credentials for simple inventory.
  Share U17 import validation/audit patterns. Depends on D01; does not run upgrade commands.

### Epic 19 - Dependency assessment and migration planning

- [x] **2026-11-10 | D07 | NEW | P1 | L - Technical dependencies and migration impact.**
  DBA/app-owner/SRE personas need the whole service boundary, beyond an active/passive label.
  **Acceptance:** link deployments, nodes and hosted databases to applications, owners, drivers,
  extensions/plugins, jobs, linked services, replicas/HA groups and backup tooling. Record OS/runtime
  and managed-provider constraints with evidence and last review. Show downstream consumers and
  unresolved dependencies before changing a shared engine; allow mixed-version transition states.
  Use U21 for teams/facets rather than rebuilding ownership; manual mapping is sufficient initially.

- [x] **2026-11-10 | D08 | NEW | P1 | L - Supported source-to-target path assessment.**
  DBA/platform personas need vendor-supported methods; app owner rejects a generic version jump.
  **Acceptance:** record source/target product, exact builds, edition, platform, topology and method
  (patch, in-place, side-by-side, dump/load or replication where supported). Link reviewed vendor
  guidance, required intermediate versions and known downgrade constraints. Classify Supported,
  Blocked or Unknown; absent rules never imply support. Treat relocation and cross-engine conversion
  as distinct change types needing their own assessment. Depends on D01/D02/D07; no generated
  executable migration scripts or assumption of zero downtime.

- [x] **2026-11-10 | D09 | NEW | P1 | L - Upgrade readiness and application certification.**
  DBA/app-owner/security personas need specific blockers and accountable approvals.
  **Acceptance:** check driver/ORM, application-vendor certification, extensions, deprecated features,
  collation/encoding, OS, edition features, backup tooling and engine compatibility state as applicable.
  Attach/reference engine-native assessment outputs with source/target/time, reviewer and remediation
  owner; missing checks are Unknown, not Passed. Summarize blocking findings without hiding details.
  Expired evidence and changed targets invalidate affected checks. Depends on D07-D08; initial delivery
  accepts reviewed evidence without promising automated compatibility analysis.

- [x] **2026-11-10 | D10 | NEW | P2 | L - Migration campaigns, owners and alternatives.**
  DBA/service-owner/procurement personas need a plan that can meet a real deadline.
  **Acceptance:** group affected deployments into waves with source/target, reason, chosen method,
  accountable DBA/app/platform owners, blockers, milestones, effort/cost estimates and change-ticket
  reference. States include Proposed/Assessing/Blocked/Ready/Scheduled/In progress/Validating/Completed/
  Rolled back/Cancelled. Compare upgrade, patch, replacement, retirement and documented support-extension
  alternatives. Store approval and change history; scheduling alone cannot clear risk. Depends on D03/D08/D09.

### Epic 20 - Rehearsal, cutover, acceptance and exceptions

- [x] **2026-11-10 | D11 | NEW | P1 | M - Rehearsal, recovery and rollback evidence.**
  DBA/SRE/app-owner personas need a tested recovery route, not just a recent backup.
  **Acceptance:** attach rehearsal results for a named source/target and representative dataset,
  method, duration, backup restore, correctness/performance checks and rollback decision points.
  Record RPO/RTO targets, measured results, prerequisites, recovery/key dependencies and test freshness.
  Define how post-cutover writes would be reconciled or lost; never imply a newer-format backup can
  always restore to an older engine. Failed/missing rehearsal blocks normal readiness or requires
  a scoped D14 exception. Reuse U11/U12 restore evidence; depends on D08-D09, integrates with D16.

- [x] **2026-11-10 | D12 | NEW | P2 | L - Scheduled change windows and cutover journal.**
  DBA/SRE/change-manager personas need coordinated work and clear stop conditions.
  **Acceptance:** plan future timezone-labelled windows, outage estimates, freeze/conflict checks,
  communication owners, approvals and runbook/checklist steps. Track externally performed actions,
  timestamps, traffic/write state, go/no-go decisions, point of no return and rollback outcome.
  Require explicit role/scope checks on approvals; allow recorded emergency exceptions via D14.
  Integrate existing maintenance suppression without hiding health or lifecycle exposure. Depends
  on D10-D11/U19; no cutover or notifications are executed merely by creating a plan.

- [x] **2026-11-10 | D13 | NEW | P1 | M - Post-change validation and version history.**
  DBA/app-owner/auditor personas need proof that the service works on the intended target.
  **Acceptance:** capture observed target/node builds and compatibility state, application tests,
  integrity checks, representative performance comparison, jobs/permissions, HA/replication and a new
  successful backup/restore-evidence requirement appropriate to policy. Assigned reviewers sign off
  within their scope; failures stay visible and completion is gated. Keep before/after evidence,
  validation time and rollback-retention deadline. A Version text edit alone cannot complete a change.
  Depends on D09-D11; uses U19 audit/U10 snapshots and supports documented D14 exceptions.

- [x] **2026-11-10 | D14 | NEW | P1 | M - Expiring support and readiness exceptions.**
  Security/change-manager/service-owner personas need explicit risk acceptance when work cannot finish.
  **Acceptance:** record affected assets, precise waived policy/gate, reason, compensating controls,
  accountable owner, permitted approver, evidence and expiry/review date. Distinguish an internal
  waiver from actual paid vendor coverage; neither changes vendor facts or clears underlying risk.
  Expiry/revocation reopens action and alerts owners. Preserve rejection/renewal history and separate
  preparer/approver where required by policy. Supports D03/D05/D09-D13; uses U19 authorization/audit.

- [x] **2026-11-10 | D15 | NEW | P1 | M - DBA lifecycle workspace and planning reports.**
  DBA/manager/auditor personas need a usable prioritized work list with evidence drill-down.
  **Acceptance:** filters for engine/branch, support phase, deadline horizon, coverage, unknown/stale
  identity, criticality, owner and campaign state; show affected deployments and hosted databases
  without double-counting cluster nodes. Report support runway, patch-baseline coverage, expiring
  exceptions and blocked/late migrations. Separate backup health from lifecycle/security/readiness.
  Display source freshness, denominator and unknown counts; share/export the same scoped results.
  Lifecycle MVP depends on D01-D04; campaign/advisory columns follow their respective models, using U05/U09/U10.

### Epic 21 - DBA recovery and operational assurance

- [x] **2026-11-10 | D16 | NEW | P1 | L - Recovery objectives and backup-chain coverage.**
  DBA/SRE/service-owner personas need to know achievable data loss and recovery time.
  **Acceptance:** record business RPO/RTO, engine-specific backup-type cadence and required base/log/
  WAL/binlog/archival dependencies where applicable. Track recoverable point/window from sufficient
  evidence, missing chain members and measured restore duration; unknown chain coverage stays Unknown.
  A fresh log backup does not prove a valid base or end-to-end recovery. Support encryption-key and
  offsite-copy dependencies. Extends U01/U04/U11/U12/U21 with chain-level semantics; no automatic restore
  execution or RPO compliance claim from timestamps alone.

- [x] **2026-11-10 | D17 | NEW | P2 | M - Backup-copy availability and retention evidence.**
  DBA/security/auditor personas need evidence that required recovery copies still exist.
  **Acceptance:** track storage/copy locations, offsite/immutable/encryption expectations, observed
  presence and last verification with accessible/cannot verify/missing distinctions. Compare retained
  recovery coverage with policy and legal-hold references; historical discovery alone is not current
  availability or proof of immutability. Surface gaps and stale checks; mask sensitive paths by role.
  Reuse U01 evidence/U21 policies. No automatic backup deletion or storage-provider reconfiguration.

- [x] **2026-11-10 | D18 | NEW | P2 | M - Integrity, HA and maintenance-job evidence.**
  DBA/SRE personas need to track assurance beyond whether a port accepts a connection.
  **Acceptance:** record/import integrity-check, replication/failover-drill and required maintenance-job
  results with node/deployment, tool/version, schedule, run time, source and outcomes. Flag missing,
  failed and stale checks separately from backup health. Allow reviewed external monitoring/ticket
  links first; distinguish a recorded result from continuously measured HA health. Depends on D07
  and U19; does not run repairs, index rebuilds, failovers or arbitrary SQL jobs.

- [x] **2026-11-10 | D19 | NEW | P3 | M - Capacity and performance planning evidence.**
  DBA/platform/service-owner personas need migration headroom and regression awareness.
  **Acceptance:** import/reference timestamped database/storage growth, free space, workload and
  performance baselines with source, units, measurement period and freshness. Record migration space
  estimates and compare pre/post representative workloads; forecasts expose assumptions and missing
  data rather than promising a precise exhaustion date. Link actions to a campaign. Integrate existing
  monitoring instead of adding a continuous query profiler or automatic tuning. Supports D09/D11/D13.

- [x] **2026-11-10 | D20 | NEW | P2 | M - Certificate and recovery-key dependency reminders.**
  DBA/security/platform personas need to avoid outages and unusable encrypted backups.
  **Acceptance:** record certificate expiry, ownership and secret-store references for TLS and
  recovery-key/certificate dependencies as applicable; show verified backup/restore access and last
  check without storing private material. Notify relevant owners before deadlines and track renewal
  evidence across replicas/clients. Distinguish key rotation from certificate expiry; integrate approved
  secret-management systems. No passwords/private keys in inventory, CSV exports or reports.

### Epic 22 - Support entitlement and safe retirement

- [x] **2026-11-10 | D21 | NEW | P2 | M - Support contracts and edition-cost readiness.**
  Procurement/manager/DBA personas need verified coverage and target-edition implications.
  **Acceptance:** link support agreement references to products/deployments, coverage type, dates,
  owner and renewal evidence; flag missing/expired entitlement separately from product lifecycle.
  Capture reviewed target-edition feature/licensing constraints and estimated extended-support or
  migration cost with currency/date/assumptions. Restrict commercial evidence appropriately; no
  automatic legal/license compliance certification. Depends on D01-D03; feeds D10 alternatives/D14.

- [x] **2026-11-10 | D22 | NEW | P2 | M - Decommission and source-retirement checklist.**
  DBA/app-owner/auditor personas need a safe end state after replacement or migration.
  **Acceptance:** validate consumers moved, jobs/replicas removed or redirected, final recoverable
  backup/key evidence retained, rollback window closed and retention/hold obligations reviewed.
  Record application/platform sign-off, externally performed access/monitoring cleanup and cost/
  contract review. Unresolved dependencies block normal retirement or require a scoped exception.
  Archive only after gates pass; preserve lineage and history. Extends U14 archive recovery with
  retirement workflow, depends on D07/D13/D16-D17/D20-D21; no automated data or backup deletion.

### Dated scope extensions and implementation order

- 2026-11-10 | UPDATE-010 | Extends U21: D01/D07 add engine/deployment/node identity and technical dependency evidence; existing U21 team/region/policy scope is retained.
- 2026-11-10 | UPDATE-011 | Extends U01/U04/U11/U12: D11/D16/D17 add migration rehearsal, recovery objectives, backup-chain interpretation and current-copy availability; existing health/evidence proposals remain open and unchanged.
- 2026-11-10 | UPDATE-012 | Extends U08/U19: D03/D05/D12/D14 add lifecycle/security-specific actions, approvals and expiring exceptions using the shared incident/audit foundations.
- 2026-11-10 | UPDATE-013 | Extends U05/U09/U10/U22: D15 adds scoped lifecycle views and evidence-aware reporting; original reporting/trend items are preserved.
- 2026-11-10 | UPDATE-014 | Extends U14: D22 adds retirement gates before archive; archive/unarchive itself remains the original U14 proposal.
- 2026-11-10 | PLAN-001 | Start D01-D04 and lifecycle-only D15 with manual catalogue/attestation; establish U19 audit conventions early. Then D07-D14 migration readiness and D16 recovery evidence. Optional collectors/advisory integrations and wider operations follow; dependencies can constrain portions of an item without preventing an honest Unknown status.
- 2026-11-10 | VALIDATION-001 | Before closing a D item, run its acceptance criteria and the synthetic boundary/applicability/rollback/role scenarios in DBA_LIFECYCLE_REVIEW.md. Verify engine-specific behavior against current authoritative guidance. This review made documentation changes only and does not establish fleet support status or migration readiness.

## Installation roadmap - 2026-11-11

2026-11-11 | NEW-003 | Added a first-run installation wizard and IIS packaging after the owner asked
for a guided setup when DBTracker is downloaded or deployed to IIS. Persona check: Sam (admin/installer)
is the main user, since today a deploy means hand-editing appsettings.json and the app crashes on boot
when the database is unreachable; Priya (DBA) needs the wizard to prove the service identity can read
backup shares; Angela (auditor) needs setup itself recorded and no published demo password left as the
only way in; Marcus and Dev only need a working app on day one. Owner decisions: protect the wizard with
a one-time setup key; offer optional sample data plus demo accounts behind a "not for production"
warning. Found while scoping: a Production deploy today seeds no users at all, so nobody can log in.

### Epic 23 - First-run installation

- [x] **2026-11-11 | I01 | NEW | P1 | L - Setup wizard at /install.**
  **Acceptance:** an uninstalled app sends every page request to `/install` and starts no background
  workers or migrations. Step 1 requires a one-time setup key that the app writes to
  `App_Data/setup-key.txt` and its log on first start (constant-time compare, attempts rate-limited,
  key deleted on completion). Database step: SQL Server name, database name, Windows (service identity)
  or SQL login, encryption options, Test connection, create the database if missing, or use an existing
  DBTracker database by applying pending migrations without dropping data. Migration step shows
  progress. Optional sample data plus the five demo accounts, off by default, with a warning that the
  password is public. First Admin account is always required. Optional email step (ArxMail first and
  default, SMTP, or none) with a test send. Optional backup-share check that reports the running
  identity and whether it can read the path. Finish writes `App_Data/dbtracker.config.json` (secrets
  such as SQL or SMTP passwords encrypted with ASP.NET Data Protection, keys kept in App_Data), records
  an audit event, and lands on the login page without a manual IIS restart. After completion `/install`
  is closed; rerunning it means deleting the config file on the server. Existing deployments that
  already have a connection string and an Admin are treated as installed; Development and Testing keep
  today's behavior.

- [x] **2026-11-11 | I02 | NEW | P1 | S - IIS packaging and deployment guide.**
  **Acceptance:** a project `web.config` for the ASP.NET Core Module (in-process, Production), a folder
  publish profile, and `DEPLOYMENT.md` covering prerequisites (.NET 10 Hosting Bundle, IIS WebSocket
  Protocol for Blazor Server, SQL Server), app pool and site setup, App_Data write permission, SQL access
  for the app pool identity, where to find the setup key, backup-share permissions, upgrades, and how to
  rerun the wizard. `dotnet publish` with the profile succeeds.

2026-11-11 | NEW-004 | Added a Windows installer after the owner asked for a setup file that checks
prerequisites, downloads and installs what's missing (or asks), installs into IIS, then opens the setup
wizard. Persona check: Sam (admin) is the main user, replacing the manual DEPLOYMENT.md steps; Priya
(DBA) needs the app pool identity to get a SQL login; Angela (auditor) needs an install log of what was
changed. Owner decisions: MSI built with the WiX Toolset; offer SQL Server Express when no SQL Server
is found. Because an MSI cannot chain other installers, prerequisites live in a WiX bundle (Setup.exe)
that runs the MSI last.

- [x] **2026-11-11 | I03 | NEW | P1 | L - DBTracker.msi (WiX).**
  **Acceptance:** per-machine MSI with a fixed UpgradeCode and major-upgrade support; installs the
  published app to a chosen folder (default `C:\inetpub\DBTracker`); creates the app pool (No Managed
  Code) and site on a chosen free port; grants the app pool identity Modify on `App_Data`; optionally
  creates a SQL login for `IIS APPPOOL\<pool>` with dbcreator on a chosen server; launch conditions that
  name exactly what's missing (IIS, WebSocket Protocol, .NET 10 Hosting Bundle) when run without the
  bundle; finishes with "Open the setup wizard" (browser to `/install`, setup key location shown).
  Uninstall removes the site, app pool and program files but keeps `App_Data` (config, keys, setup key)
  unless the user chooses otherwise. Verbose MSI log on request. Builds with `dotnet build`, no global
  WiX install.

- [x] **2026-11-11 | I04 | NEW | P1 | M - DBTracker-Setup.exe prerequisites bundle (WiX Burn).**
  **Acceptance:** detects and, after one confirmation, installs: IIS with the WebSocket Protocol and
  required features (DISM, Windows client and Server), the .NET 10 Hosting Bundle, and optionally SQL
  Server Express when no local instance exists (or skips it so the wizard can use a remote server).
  Every download comes from Microsoft with a pinned SHA-512 taken from Microsoft's own release
  metadata at build time, never a guessed URL. Restarts IIS after the Hosting Bundle. Runs the MSI last
  and ends by opening the setup wizard. Quiet mode for unattended installs. Install log in %TEMP%.

2026-11-11 | NEW-005 | Added a system tray app installed with DBTracker after the owner asked for a
background app that opens DBTracker and performs small tasks. Persona check: Sam (admin) gains a
status light and admin controls without IIS Manager; Priya (DBA) wants an at-a-glance count of failed
and overdue backups; Angela (auditor) needs any token the tray uses to expire, be revocable and be
audited; Marcus and Dev mainly need "Open DBTracker". Owner chose all four task groups: open + status,
admin controls, folders and logs, backup risk count. The risk count needs the tray to call DBTracker as
a user, so personal access tokens are a separate web-app item (I06).

- [x] **2026-11-11 | I05 | NEW | P2 | L - DBTracker tray app.**
  **Acceptance:** .NET Framework 4.6.2 WinForms tray app (no extra runtime on Windows Server 2016+)
  installed by the MSI, started for every user at sign-in (can be turned off from its menu), launched
  un-elevated at the end of Setup, closed and removed on uninstall. Reads the site port and folders the
  MSI records under `HKLM\SOFTWARE\Architonix Labs\DBTracker`. Menu: Open DBTracker (or the setup
  wizard while setup is pending); status light (green ok, amber degraded or setup pending, red no
  response) from `/healthz` with a notification on each change; Start, Stop and Restart the app pool and
  site through a UAC prompt; Copy setup key while setup is pending; open the install folder, App_Data,
  the site's IIS log folder and the deployment guide; recent app errors from the Windows event log;
  failed / overdue / never-backed-up counts with a notification for newly at-risk instances, using a
  personal access token stored per Windows user with DPAPI. Pure logic in a netstandard2.0 library with
  unit tests.

- [x] **2026-11-11 | I06 | NEW | P2 | M - Personal access tokens and the tray API.**
  **Acceptance:** `/account/tokens` (any signed-in role) creates a named token with an expiry (default
  90 days), shows it once, lists the user's tokens with created / last used / expires, and revokes them.
  Only a SHA-256 hash is stored; tokens look like `dbt_<random>`. Create and revoke are audited.
  Deactivating a user (U16) makes their tokens stop working. `GET /api/tray/summary` accepts only a
  bearer token and returns read-only counts (Failed, Overdue, NeverBackedUp) plus the at-risk list,
  401 otherwise. Anonymous `GET /healthz` returns status (ok, degraded, setup-required), whether setup
  is done, database reachability and version, with no other details. Unit and E2E tests.

## Backlog audit - 2026-11-11

2026-11-11 | NEW-006 | The owner asked for a check of the backlog for missing features and features
that need updating. Each ticked item's acceptance text was compared with the code by reading it (no
build, test or UI run). Only I01 and I06 fully met their acceptance text; U20 was not checked in
depth. Most gaps follow one pattern: the entity and service exist, but the page never fills in or
shows the field, so the feature is stored but unusable. Findings marked ✔ were also checked by hand
in the code; the rest come from the code reading and should be confirmed before work starts.

Tracking: original items and their ticks are kept as delivery history (convention above). Each G
item reopens the named original by listing only what is still missing; close a G item when its gaps
are closed and tested, and record that as a dated entry. Priorities use the earlier P1/P2/P3 and
S/M/L definitions.

### Epic 24 - Correctness and security fixes from the audit

*Bugs and bypasses, where the app does something wrong or unsafe today. Ahead of all other open
work.*

- [x] **2026-11-11 | G01 | REOPENS D22 | P1 | S - Archive bypasses the retirement checklist.** ✔
  Angela (auditor) and Priya (DBA) rely on the checklist to stop an unsafe retirement. Today the
  plain Archive button on the instance page calls `InstanceArchiveService.ArchiveAsync` directly
  with no checklist, and a missing rollback-retention deadline returns NotApplicable, which the
  evaluator counts as satisfied (`RetirementChecklistGateEvaluator.cs`, `allSatisfied`). The Progress
  log says both cannot happen.
  **Acceptance:** decide (owner) whether plain archive stays for non-retirement cases, such as a
  record created by mistake. If it stays, label it and require a reason; if not, route it through
  the checklist. A missing rollback deadline becomes Unknown unless the instance has no change
  history. The checklist is linked to its campaign so the rollback gate can apply. Tests for both
  paths.

- [x] **2026-11-11 | G02 | REOPENS U07 | P1 | S - New users' first alerts arrive already read.** ✔
  Priya and Noor (on-call) would miss the first alerts after a fresh install.
  `NotificationService.EnsureBackfilledAsync` writes no read-state marker when a user's first visit
  finds zero notifications, so every notification that arrives later is backfilled as read.
  **Acceptance:** the backfill runs once per user, even when nothing exists to mark yet, and only
  marks notifications created before that first visit. A test covers a fresh install followed by an
  alert.

- [x] **2026-11-11 | G03 | REOPENS D12 | P1 | S - Change windows can complete without approval.** ✔
  The change manager's approvals gate only Approved and InProgress. A Planned window can be set
  straight to Completed or RolledBack, and the status menu offers every status.
  **Acceptance:** define the allowed status transitions. Completed and RolledBack require a window
  that was approved and InProgress, or an active D14 emergency exception. The UI offers only the
  allowed next states, and the server enforces them.

- [x] **2026-11-11 | G04 | REOPENS U17 | P1 | M - Bulk edit writes unchecked values.** ✔
  Sam (admin) and Angela need a reviewed, attributable batch.
  **Acceptance:**
  - `InstanceBulkEditService.ApplyAsync` runs `InstanceValidator` (it always returns no
    validation errors today) and rechecks permission.
  - It detects instances changed since the preview and skips or flags them.
  - Audit rows are written in the same save as the changes and share a batch id, so a failed save
    leaves no audit row.

- [x] **2026-11-11 | G05 | REOPENS U16 | P1 | M - Invitations, temporary passwords and downgrades.** ✔
  Sam and Angela.
  - `SendInviteAsync` emails the temporary password in plain text; the acceptance asks for an
    expiring single-use link.
  - The forced password change is a one-time redirect after sign-in, so the user can navigate away.
  - The security stamp rotates only for Admin to non-Admin, so DBA to Viewer keeps DBA rights until
    the next sign-in. Device links are not revoked on any downgrade.
  - Creating a user is audited only when the invite email is sent.

  **Acceptance:** invite by an expiring single-use set-password link (reuse the reset-token
  plumbing). A must-change-password user is held on that page on every request. Any role change
  rotates the stamp and revokes pending device links. User creation is always audited.

- [x] **2026-11-11 | G06 | REOPENS D21 | P1 | S - Cost figures leak through the audit log.** ✔
  Procurement and Marcus need commercial figures limited to `CommercialEvidencePolicy`.
  `EditionCostEstimateRecorded` writes the amount and currency into the audit reason, and `/audit`
  plus its CSV are open to Auditor.
  **Acceptance:** the audit reason names the estimate, not its figures, or the figures are masked
  for roles outside the policy on the Audit page and in its export. Existing audit rows are masked
  on display. The Progress log says a DBA can record a contract, but the form is Admin/ITManager
  only: settle which is intended.

- [x] **2026-11-11 | G07 | REOPENS U01 | P1 | S - A rewritten backup file of the same size is ignored.** ✔
  Angela and Priya: the scanner's dedup signature is file name plus size, so a nightly file
  overwritten under the same name and size is never recorded again.
  **Acceptance:** add the file's last-write time to the signature, and update the unique index with
  a migration. Legacy grandfathered records keep working. Discovered files can be given a type
  (Full, Differential, Log) by file-name rule instead of always Full.

- [x] **2026-11-11 | G08 | REOPENS I05 | P2 | S - Tray token left on disk after uninstall.**
  Angela: each user's DPAPI-protected access token (`%LOCALAPPDATA%\DBTracker\Tray\token.dat`) and
  the HKCU preference outlive the uninstall.
  **Acceptance:** say plainly in the uninstall text and DEPLOYMENT.md that tokens must be revoked
  at `/account/tokens`. Optionally, the tray deletes its own files on first start once the app is
  gone. An MSI cannot reach other users' profiles reliably.

- [x] **2026-11-11 | G24 | REOPENS U17 | P1 | M - Re-import resets fields the file doesn't contain.**
  Found while building G23. An Update row was saved as a whole new record, so every re-import
  silently reset the matched instance's other fields:
  - owner team, region, cluster, replica set, criticality and backup contact
  - file-name patterns, scan status, applied policy and connectivity overrides
  - creation time and archive state, so an archived instance came back

  It also ignored edits made since the preview, and two instances sharing a name made the whole
  file fail with "Couldn't read". Sam, Angela and Priya are all affected.
  **Acceptance:**
  - Updates load the stored record and copy only the importer's columns, with the preview-time
    row version, so a concurrent edit fails the save.
  - A row matching an archived instance, or several instances, is shown as ambiguous with the
    reason and never applied.
  - Unit and E2E tests prove the untouched fields and the archive state survive.

#### Epic 24 delivery - 2026-11-11

Each item was checked against the personas before it was built; the decisions are noted below.

- **G01:**
  - **Personas.** Sam needs to remove a duplicate without a 10-gate retirement. Angela and Priya need
    a real decommission to go through the checklist.
  - **Archive.** The archive panel asks why: "Duplicate, or entered by mistake" archives directly
    with a reason. "Being decommissioned or replaced" points to the retirement checklist.
    `InstanceArchiveService.ArchiveAsync` refuses while a checklist is open, and only
    `ArchiveForRetirementAsync` (called by the checklist) archives a retirement.
  - **Checklist.** Opening `/instances/{id}/retirement` no longer creates a checklist; "Start
    retirement checklist" does. A checklist can be cancelled with a reason, and is kept for history.
  - **Rollback gate.** It now looks at change validations from any campaign. It is NotApplicable
    only when the instance has no validation at all, and Unknown when a change exists without a
    deadline.
  - **Audit and migration.** The audit reason says "Not a retirement" or "Retired through checklist
    #n". Migration `AddRetirementChecklistCancellation`.
- **G02:**
  - **Fix.** `NotificationInboxStarts` records each user's first inbox visit once, even when nothing
    exists to mark. Only notifications created before that visit are pre-marked read; later ones
    arrive unread.
  - **Personas.** Noor keeps the "no backlog of old alerts on day one" behavior.
  - **Migration.** `AddNotificationInboxStart`.
- **G03:**
  - **Transitions.** `ChangeWindowStatusTransitionValidator` allows Planned to Approved, InProgress
    or Cancelled; Approved to InProgress, Planned or Cancelled; and InProgress to Completed or
    RolledBack. Completed, RolledBack and Cancelled are final. The page offers only the allowed next
    statuses, and the server refuses others. Planned to InProgress still needs approvals or an
    active emergency exception.
  - **Also found.** A window could be planned with no required approvals, which made the gate pass
    trivially. Planning now needs at least one.
- **G04:**
  - **Apply.** Bulk edit is all or nothing. It validates the edited fields with `InstanceValidator`
    and names each instance and error. An older, unrelated problem on a record doesn't block the
    batch.
  - **Checks.** Apply skips and names instances changed since the preview (row version), and
    rechecks the actor's role and active status in the database.
  - **Audit.** Audit rows are written in the same save as the edits, under "Bulk edit batch
    <id>".
  - **Also found.** Apply used whatever was in the form at click time; it now applies exactly the
    previewed values.
- **G05:**
  - **Invites.** They email a single-use set-password link (24 hours) and never the password. The
    reset page reads "Set your password" for invites, and if mail isn't sent the admin shares the
    temporary password in person.
  - **Forced change.** `MustChangePasswordGuard` in the main layout returns a must-change user to
    `/account/change-password` on full loads and on in-app navigation.
  - **Role changes.** Every role change rotates the security stamp and cancels approved device
    links; sessions still pick it up within the 30-minute revalidation window.
  - **Audit.** User creation is audited (`UserCreated`) whether or not an invite is sent.
- **G06:**
  - **Audit.** New cost-estimate audit rows carry no figures. Older rows are masked on `/audit` and
    in its CSV for roles outside `CommercialEvidencePolicy`, and audit search ignores those rows'
    reason text, so an amount can't be found by searching for it.
  - **Roles.** Support contracts (no amounts) now use a new `ContractEvidencePolicy`
    (Admin, DBA, IT Manager), so the Progress log statement is true. Marcus keeps access, since he
    owns vendor relationships.
- **G07:**
  - **Signature.** The scanner signature is name + size + last-write time (stored as the record's
    TimestampUtc, so existing records still match and nothing is re-recorded on upgrade). A file
    rewritten at the same size is new evidence.
  - **Type.** Discovered files get a type from the name (`BackupFileTypeClassifier`): `.trn` or a
    log/tlog/trn name part means a log backup, `.dif` or a diff/differential part a differential,
    anything else full. The scan preview shows "Would be recorded as ...".
  - **Migration.** `WidenDiscoveredBackupSignature`.
- **G08:**
  - **Docs.** DEPLOYMENT.md now says tray tokens and preferences survive uninstall, and how to
    revoke them at `/account/tokens`.
  - **Not done.** No installer change: deleting other users' profile files from an MSI is
    unreliable, and the file is readable only by its Windows user and expires.

Tests added: unit tests for the transition validator, rollback gate, file-type classifier,
commercial audit masking and invite text. E2E tests for each item (archive refused while a checklist
is open, then allowed after cancel; first inbox visit; change-window transitions; bulk edit
validation and batch audit; forced password change on in-app navigation; cost figures hidden from
the auditor and from search; DBA contract access; same-size rewrite and log typing). Existing E2E
tests were updated for the archive choice and the explicit checklist start.

- 2026-11-11 | UPDATE-026 | Closes G01-G08 (Epic 24), G19 and G24 after their acceptance checks: 664 unit tests and the full E2E suite (109 of 109 before G23/G24, then the import, instance and ownership tests after them) pass. G19: `installeruild.ps1` writes `SHA256SUMS.txt` (sha256sum format) and SETUP-GUIDE.md gives a one-line check, verified against a real build output. G23 is partly delivered: the instance template now has an example row, allowed values as `#` comment lines (ignored on import), a column guide on the import page, and the 12 missing columns (Region, Cluster, ReplicaSet, Criticality, OwnerTeam by name, BackupContactName/Email/Phone, BackupFileNamePatterns, ConnectivityHost/Port, RestoreTestIntervalDays). The backup and restore history import is still to do, so G23 stays open. Also from G12: InstanceValidator now rejects a connectivity port outside 1-65535 instead of silently using the default.
- 2026-11-11 | UPDATE-027 | Progress on G16, G20, G21 and G22. Persona notes are inline.
  - **G16 (partial).**
    - Port check: on a first install it now also refuses a port any program is listening on, even
      when IIS config is readable (Sam).
    - Hosting Bundle detection: Setup accepts any 20.x ASP.NET Core Module at or above the pinned
      build, so a server on a newer .NET 10 patch is not given an older bundle and an IIS restart. A
      21.x module still needs the pinned runtime folder.
    - SQL Express: the Options hint says that ticking it accepts Microsoft's license terms (Angela).
      The download note says the SQL Express installer fetches its own media.
    - Tray: Admin > Restart the site added (stop then start), with a tray test.
    - Docs: DEPLOYMENT.md hash statements corrected.
    - Kept: the two-poll delay before a tray status notification (avoids noise on app pool recycles;
      Sam). The guide already says "when the state changes and stays changed".
    - Still open: a keep-data checkbox in the MSI's own uninstall screens (the default keeps data, and
      full removal is `RemoveData=1`). All installer changes still need a Hyper-V run (G18).
  - **G20 (partial).**
    - Built: the app no longer redirects `/healthz` and `/api/tray` to HTTPS when the request comes
      from the same machine (`HttpsRedirectExemption`, unit tested), so adding an HTTPS binding
      doesn't break the tray (Sam). The token never crosses the network over HTTP (Angela).
    - Docs: DEPLOYMENT.md explains adding the binding.
    - Still open: choosing a certificate and creating the HTTPS binding inside the MSI.
  - **G21 (partial).**
    - Code platform: `tools/scan.sh` runs the shared code-platform scan (SonarQube, Dependency-Track,
      DefectDojo) through `DockerSecurity/code-platform/scan-project.sh`, with no tokens in the repo.
    - CI: `.github/workflows/ci.yml` builds, runs unit and tray tests and produces a CycloneDX SBOM
      (command verified locally with CycloneDX 6.3.1).
    - Logs: `tools/Ship-IisLogs.ps1` sends the site's IIS W3C logs to `/srv/weblogs/dbtracker/` for
      GoAccess, and DEPLOYMENT.md explains scheduling it.
    - README has a "Quality checks" section.
    - Still open: the first scan itself, because Docker Desktop was not running on this machine.
  - **G22 (partial).**
    - Verified: `dotnet publish src/DBTracker.Web -p:PublishProfile=FolderProfile` succeeds and the
      output has `web.config` and no Development or Testing settings.
    - Still open: the tray checks (autostart, admin prompts, closing on uninstall), which need the
      Hyper-V desktop run with G18.
- 2026-11-11 | UPDATE-028 | Delivery of G09-G15 and the rest of G23 (four parallel tracks, each
  checked against the personas, merged into `backlog/audit-fixes` with one migration per track).
  Details are in each track's merge commit.
  - **G09 (track A):**
    - Scanners: a Disabled state, a stored last success, and a dashboard link to the affected Scan
      Activity rows.
    - Bell: pause and an error state.
    - Evidence labels in alerts, exports, compliance and the management report.
    - Saved views can be renamed, and the Reports range is kept in the URL.
    - Instance list filters, sorts and pages in SQL, with latest-only loads for reports.
    - Benchmark recorded (see G32 for targets).
  - **G10 (track A):**
    - Team subscriptions and channel-aware inbox.
    - Per-user quiet hours: email only, with escalation still sent.
    - Recovery emails, and incident acknowledge and assign are audited.
    - Every send stored as a delivery attempt, shown on `/setup`.
  - **G11 (track B):**
    - Snapshot policy fingerprint and evidence provenance. Frozen rows carry evidence level and
      restore recency.
    - Viewer-zone times with UTC beside them, and the zone stored on entries.
    - Restore evidence references.
    - Trends: period-bound snapshots, policy changes, restore recency and drill-down. Caption bug
      fixed.
  - **G12 (track B):**
    - Conflict review with field-level merge.
    - Real archive-preview report line; unarchive needs a reason.
    - Import matches by Name or Id, with an inventory export and fixable error rows.
    - Audit before/after on create, edit and import; `/audit` date filter, full export and stated
      retention.
    - Policy edits are versioned with an impact preview.
  - **G13 (track C):**
    - Full catalogue fields and revision history; expired overrides ignored.
    - Milestone-based notice dedupe; Unknown and Needs review kept separate; ESU needs an active
      Extended contract; edition- and channel-aware matching (fixes MySQL 8.0 getting 8.4 dates).
    - Required-by and build comparison on baselines.
    - Advisory review records owner, deadline and verification, with multi-factor ranking.
    - Failed version reads recorded. CSV imports for the catalogue, advisories and version metadata
      at `/lifecycle/import`.
  - **G14 (track C):**
    - Dependency evidence displayed.
    - Full path-assessment fields, merged with the matched rule.
    - Check references and custom checks.
    - Campaign milestones, effort and cost editable, with cost masked for DBAs; alternatives have
      effort, cost and risk.
    - Approvals gate Scheduled and In progress. A passing rehearsal is required for Ready,
      Scheduled and In progress. RPO/RTO is compared with targets, and NotPerformed no longer
      passes.
  - **G15 (track D):**
    - Change freezes (Admin and IT Manager) and runbook steps.
    - Account-bound approvals and sign-offs, with the two required scopes kept separate.
    - Linked post-change evidence and before/after snapshots.
    - Exception target picker, revoke reason and owner routing, chosen renewal expiry and history.
    - Workspace branch and owner filters, hosted databases and late detection; CSV header lines.
    - WAL and binlog chain roles.
    - Legal hold, and offsite, immutable and encryption checks.
    - Required maintenance checks, and imports at `/assurance/import`.
    - Capacity limit and pre/post comparison.
    - Certificate renewal and rotation history, with notices to the certificate owner.
  - **G23 (track B):** a backup and restore history import at `/instances/import-history` with an
    explicit zone per time, preview, rows-to-fix download and repeat-safe matching. Imported entries
    are labelled Imported and never count as verified or restore-tested.
  - **Migrations:**
    - `AddScanSuccessDeliveryAttemptsAndNotificationPreferences`
    - `AddImportedEvidenceAndEventTimeZones`
    - `AddLifecycleNoticeMilestonesAndCatalogueFields`
    - `AddChangeFreezesAssuranceAndCertificateHistory`
  - **Merge conflicts:** tracks A and B conflicted on evidence classification, compliance and
    Reports. They were resolved so that imported evidence short-circuits and only verified
    (non-imported) restores upgrade a backup.
  - **After deploy:** the first support-runway sweep sends one Unknown or Needs review notice for
    each instance in that state.
- 2026-11-11 | UPDATE-029 | G25 and G27 delivered, following the installed `modern-web-guidance`
  skill (Google Chrome) guides `security`, `performance` and `visually-stable-font-fallbacks`.
  - **G25:**
    - CSP with a per-request nonce and `'strict-dynamic'`, `object-src 'none'`, `base-uri 'self'`
      (needed by Blazor), `frame-ancestors 'none'` and `form-action 'self'`. Trusted Types is
      report-only.
    - Companion headers, a `/csp-reports` log without query strings, and `Clear-Site-Data` on
      logout.
    - A `CspScript` component stamps the nonce on component script tags.
    - The E2E test proves no CSP violations and no third-party requests on the main pages. It first
      caught the unnonced QR sign-in script.
  - **G27:**
    - Unused `bootstrap.bundle.min.js` removed.
    - Inter and IBM Plex Mono self-hosted (latin and latin-ext WOFF2 with OFL texts), Inter latin
      preloaded with `crossorigin`, and `font-size-adjust: from-font`.
  - **Still open:** the LCP before/after numbers belong to G28.
- 2026-11-11 | UPDATE-030 | Closes G09-G15, G23, G25 and G27. Full E2E run on the merged tracks: 127 of 135 passed. The 8 failures were fixed and all 8 rerun green. Two were real bugs: (1) Razor rendered "v@p.Version" literally on the Ownership and Apply policy pages, because Razor reads `v@p` as an email address; it is now `v@(p.Version)`. (2) A backup or restore logged with the untouched "now" default was stored rounded down to the minute, up to 59 seconds early, so it could fall before a change start and be excluded as post-change evidence; it now means the actual current time. The rest were test issues: two tests clicked the first submit button on the page, which is the sidebar sign-out; a caption and an alert text had changed wording; a one-shot click raced the circuit; and the paging test assumed a single page. 988 unit tests pass.
- 2026-11-12 | UPDATE-031 | G26 and G29-G31 delivered (tracks E and F, following the installed modern-web-guidance
  skill's passkey, dialog, popover, menu, toggletip and container-query guides).
  - **G26 (passkeys):**
    - `/account/passkeys` adds, renames and removes passkeys, and every change is audited. Sign-in has a "Sign in with a
      passkey" button and passkey autofill on the email field.
    - Built on ASP.NET Core Identity's .NET 10 passkey support (schema version 3, migration `AddPasskeys` adds only
      `AspNetUserPasskeys`), with user verification required.
    - `webauthn-polyfills@0.1.7` (Apache-2.0) is served from wwwroot, not a CDN.
    - Deactivated accounts are refused and must-change-password users are still sent to change it. Both are proven by
      E2E with a Chrome virtual authenticator.
    - `Passkeys:ServerDomain` is documented in DEPLOYMENT.md. Passkeys need HTTPS or localhost.
  - **G29:** a `ModalDialog` component (`<dialog>` with `showModal()`) for archive, cancel retirement checklist, revoke
    access token, and exception revoke and renew. Focus returns to the opener, Esc closes, and errors show inside the
    dialog. Revoking a token now asks for confirmation.
  - **G30:**
    - The notification menu is a `popover="auto"`.
    - A `Toggletip` component replaces title-only explanations; the 13 remaining titles only repeat visible text or
      aria-labels.
    - Anchor positioning sits behind `@supports`, with a script fallback. Each anchor is named explicitly
      (`anchor-name`/`position-anchor`) after E2E showed the implicit popovertarget anchor isn't reliable.
    - Polyfills from a CDN were not used, to keep the no-third-party rule (G27).
  - **G31:** container queries for the stat strip, detail-card rows and key-value lists, with the old media queries as the
    fallback.
- 2026-11-12 | UPDATE-032 | G28 delivered: lab Core Web Vitals baseline with `tests/DBTracker.E2E/Benchmarks/CoreWebVitalsBaseline.cs`.
  It is `[Explicit]`, uses PerformanceObserver for LCP, CLS, Event Timing and long animation frames, and tops the inventory
  up to 1,000 instances with 30 days of backups.
  - **Reports:** it didn't finish loading within 30 s, because Backup activity rendered every run (about 7,000 rows). It now
    shows the newest 200, with "Show 200 more" and a "failed and missed only" switch; the export still has every run.
  - **Dashboard:** it took 7.9 s to be ready on the phone profile (LCP 2.9 s), because it rendered every instance. It now
    shows the 25 most at risk, with a link to the full list in Instances.
  - **Results after both fixes:**

| Profile | Page | Ready ms | LCP ms | CLS | Slowest interaction ms |
|---|---|---|---|---|---|
| Desktop | Dashboard | 164 | 153 | 0.000 | 24 |
| Desktop | Instances | 400 | 320 | 0.009 | under 16 |
| Desktop | Reports | 384 | 273 | 0.000 | 16 |
| Desktop | Instance detail | 83 | 130 | 0.008 | 16 |
| Desktop | New instance form | 83 | 50 | 0.000 | under 16 |
| Phone 360 px, 4x CPU | Dashboard | 516 | 284 | 0.000 | 56 |
| Phone 360 px, 4x CPU | Instances | 690 | 338 | 0.042 | under 16 |
| Phone 360 px, 4x CPU | Reports | 1,608 | 670 | 0.000 | 96 |
| Phone 360 px, 4x CPU | Instance detail | 473 | 761 | 0.110 | 32 |
| Phone 360 px, 4x CPU | New instance form | 336 | 194 | 0.000 | under 16 |

  - **Against the "good" limits:** every LCP is under 2.5 s and every interaction under 200 ms. CLS is under 0.1 except
    instance detail on the phone (0.110), opened as G33.
  - **Other fixes from the post-merge E2E run:** popover anchors, and four test issues: a test user's display name
    containing "Deactivated", a sign-in selector, a 360 px row click, and the inbox tests' menu close.
- 2026-11-12 | UPDATE-033 | G32 delivered (track G), except the owner's decision on U06 response-time targets.
  - **Delivery and audit:**
    - Webhook sends are stored as delivery attempts; the URL is never stored, and nothing is written when no webhook is set.
    - The alert worker audits incident open, escalate and close as system actions.
  - **Inbox:** filtering, counts and paging now run in SQL, matching the in-app rules exactly (unit-tested).
  - **Report errors:** non-Admin report senders get a next step they can take; only Admins are pointed to `/setup`.
  - **Time zones:** no server-zone `ToLocalTime()` is left in the Web project.
  - **Evidence:**
    - Manual backup and restore entries are audited with their entered values.
    - Restore corrections are honoured for restore-tested, while as-of views ignore later corrections.
  - **Lifecycle:** instances with no version identity get one Unknown notice. The dedupe state is the audit record, not an
    invented identity, to keep Angela's provenance rule.
  - **Assurance:** the retirement gate counts hosted databases, and maintenance results can be backdated.
  - **CSV:** one CSV reader (`CsvRecordSplitter` removed).
  - **Checks:** no migration needed; 1053 unit tests pass. New findings are in G34.
- 2026-11-11 | UPDATE-025 | Clarifies the Progress log's D22 entry (see UPDATE-022): after G01, "NotApplicable is not treated as passing" is replaced by "NotApplicable only when proven" and "no second archive path" holds for retirements. The D21 entry's "a DBA can record a contract" is true after G06. Earlier wording is kept as history.

### Epic 25 - Finish partly built features

*Ticked items whose acceptance text is only partly met. Grouped by area; the gaps listed are the
remaining scope of each original item.*

- [x] **2026-11-11 | G09 | REOPENS U02, U03, U04, U05, U06 | P2 | M - Monitoring and browsing gaps.**
  - U02: there is no Disabled scanner state (interval 0 shows Healthy), and last success is lost
    after a failed scan because one `LastScanUtc` is stored. The banner links to the whole report,
    not the affected rows.
  - U03: the notification bell has no pause control and no visible error state.
  - U04: evidence levels appear only on instance detail, not in alerts, report exports or
    compliance rows.
  - U05: saved views cannot be renamed (`RenameAsync` has no caller). The Reports date range and
    custom dates are not in the URL.
  - U06 ✔: `GetPagedAsync` loads every summary and then pages in memory, and Reports and Compliance
    still load full histories. No benchmark exists: run the 1,000-instance / 100,000-backup baseline
    from USABILITY_REVIEW.md before choosing targets.

- [x] **2026-11-11 | G10 | REOPENS U07, U08, U09, U15 | P2 | M - Notifications, incidents and delivery records.**
  - U07: the UI cannot subscribe by team (the matcher supports it), and the inbox ignores the
    channel, so an email-only subscription also shows in-app.
  - U08: there is no quiet hours setting, acknowledge and assign write no audit event, and recovery
    is in-app only.
  - U09/U15: send attempts are not stored (only a toast), and /setup shows no attempt history. Some
    "next action" links point back to /setup.

  **Acceptance:** a persisted send-attempt record (time, channel, scope, recipients count, outcome,
  error) shown on /setup. Incident actions audited through U19.

- [x] **2026-11-11 | G11 | REOPENS U10, U11, U12, U22 | P2 | M - Evidence time, snapshots and trends.**
  - U10: `PolicyVersion` is always `default-v1`. Evidence provenance is one fixed string, and frozen
    rows carry no evidence level.
  - U10/U11: times use the server's zone without naming it. Show and store the zone, or use UTC with
    the viewer's local time beside it.
  - U12: restores have no evidence-reference field.
  - U22: Trends lacks policy changes, drill-down links and Current/Overdue restore recency. Its
    period snapshot can predate the period start.
  - Bug: the Backup Activity caption says "in the last N day(s)" when a custom range is active.

- [x] **2026-11-11 | G12 | REOPENS U13, U14, U17, U18, U19, U21 | P2 | M - Editing, archive, import and audit.**
  - U13: a conflict offers reload or "Save anyway", with no view of the other change.
  - U14: the archive preview's report line is hard-coded, and unarchive takes no reason.
  - U17: import corrections download only Row and Error, not the row data to fix; the matching rule
    is fixed to Name.
  - U18: no port range validation, so an invalid port silently falls back to the default.
  - U19: create, edit and import record no before/after; /audit neither shows nor exports it.
    Export covers only the 200 on-screen rows, with no date filter, and retention is undocumented.
  - U21: policies can never be edited, so every policy stays at version 1, and there is no impact
    preview of a policy edit.

- [x] **2026-11-11 | G13 | REOPENS D01-D06 | P2 | L - Lifecycle catalogue and patch facts.**
  The data model is in place; most of the work is in the pages.
  - D01: hosting context is always empty.
  - D02: there is no catalogue import. The form omits build, patch train, edition, distribution,
    applicability, ESU/GA dates, date precision and source. Revision history is never shown.
    Expired overrides are checked only on Refresh, so runway and baselines keep using them. Nothing
    separates vendor facts from internal policy.
  - D03: notices do not re-fire after a date revision (`LastRunwayNoticeThresholdDays` is never
    reset). Support contracts are never consulted, and Unknown and Needs-review are merged and
    skipped. Catalogue matching ignores edition and channel.
  - D04: no Required-by or release-notes fields, and no build-level comparison.
  - D05 ✔: review saves an empty owner, no deadline, no target patch and status Verified. Ordering
    is by severity only, and there is no advisory import.
  - D06: no metadata import, and permission failures are never recorded.

- [x] **2026-11-11 | G14 | REOPENS D07-D11 | P2 | L - Migration planning inputs and gates.**
  - D07: last review and evidence are not displayed.
  - D08 ✔: edition, platform, topology, guidance, intermediate versions and downgrade constraints
    are always passed empty, and a matched rule's values are not copied or shown.
  - D09: source/target references and remediation notes are always empty; custom checks have no UI.
  - D10 ✔: no milestones, blockers, effort or cost entry; alternatives have no effort, cost or risk;
    approval notes are always empty, and approvals gate nothing.
  - D11: a missing rehearsal blocks only Completed, not Ready, Scheduled or InProgress. RPO/RTO
    results are never compared with targets, and a NotPerformed performance check passes.

- [x] **2026-11-11 | G15 | REOPENS D12-D20 | P2 | L - Change, recovery and assurance gaps.**
  - D12: runbook steps have no UI; the freeze check is only overlap with other windows; the journal
    time is always "now".
  - D13: sign-offs are typed names with no role check; before/after evidence and linked backup or
    restore ids are never written.
  - D14: revocation alerts nobody, and expiry notices are not routed to the owner; affected assets
    are hand-typed ids; renewal history is not viewable, and renewal hardcodes 30 days.
  - D15: no branch or owner filter, no hosted databases, "late" is not detected, and the CSV has no
    denominator or freshness.
  - D16: `ChainRole` is never set, so WAL, binlog and archive requirements can never be met.
  - D17: no legal-hold input, and no encryption, offsite or immutable expectation check.
  - D18: no import, and a never-recorded required check is not flagged.
  - D19: no pre/post comparison UI, no campaign link, and the capacity limit is not saved.
  - D20: no renew or rotate path; notices go to the instance contact, not the certificate owner.

- [ ] **2026-11-11 | G16 | REOPENS I03, I04, I05 | P2 | M - Installer and tray gaps.**
  - I03: the port check ignores non-IIS listeners when IIS config is readable; keep-data on
    uninstall is command-line only.
  - I04: only the Hosting Bundle hash comes from Microsoft metadata. SQL Express is a bootstrapper
    hashed at build time that downloads unpinned media, and its licence terms are accepted
    silently. The Hosting Bundle detection needs exactly 10.0.15, so a newer 10.0.x is reinstalled
    over.
  - I05: no site restart, although CHANGELOG says so. A status change notifies only after two
    polls, and the first state is never shown.
  - Fix the I04 wording, DEPLOYMENT.md and the installer text to match the hash behavior; the
    CHANGELOG wording is already correct.
  - **2026-11-12 progress: keep-data choice in Setup.exe.**
    - Built: the bundle's Modify page (also what Settings > Apps > Uninstall shows) has an **Also
      delete DBTracker's data in App_Data** checkbox, off by default, bound to the existing
      `RemoveData` variable, so ticking it passes `REMOVE_DATA=1` to the MSI. The text says what goes
      (saved configuration, setup key, encryption keys), that it can't be undone and the wizard runs
      again, and that the SQL database and login are not deleted (checked against `RemoveAppData`:
      it deletes only `<site folder>\App_Data` and the then-empty site folder).
    - Kept: `/uninstall /quiet` without `RemoveData=1` still keeps the data (Sam). Deleting is only an
      explicit tick or `RemoveData=1` (Angela, Priya).
    - Still open: clicking through the page in the Hyper-V VM (G18). The MSI's own maintenance
      dialogs still have no checkbox; `REMOVE_DATA=1` stays command-line only there.

### Epic 26 - Release hardening and engineering standards

*Not in the backlog before this audit. Sam (admin/installer) is the main user; Angela needs signed,
verifiable downloads.*

- [ ] **2026-11-11 | G17 | NEW | P1 | M - Code signing.** Sign Setup.exe, the MSI, the custom
  action DLL and the tray app so SmartScreen and Smart App Control accept them. Remove the known
  issue from CHANGELOG once shipped.

- [ ] **2026-11-11 | G18 | NEW | P1 | M - Untested installer paths.** Click through the Setup.exe
  Options page (SQL Express checkbox), run a version-to-version major upgrade (data and config
  kept), and test Hosting Bundle before IIS (500.19, see DEPLOYMENT.md). Test on Windows Server 2016
  or 2019 and on Windows 10/11, which SETUP-GUIDE.md claims; only Server 2025 has been tested.
  Rerun `Start-HyperVTest.ps1` for each.
  - **2026-11-12 progress: major upgrade verified.** `installer/test/Test-HyperVUpgrade.ps1` installed 1.1.1 over
    1.1.0 on Server 2025. The version moved forward, the setup key and data-protection keys were kept (same hash), and
    `/healthz` answered 200 before and after. The uninstall test phases that follow all passed: keep data, then full
    removal, then reinstall. The first run (1.1.0 over 1.0.1) found G35. Still open: the Options page click-through,
    Hosting Bundle before IIS, and the older Windows versions.

- [x] **2026-11-11 | G19 | NEW | P1 | S - Release checksums.** SETUP-GUIDE.md tells users to
  download `SHA256SUMS.txt`, but nothing generates it. `installer/build.ps1` writes it next to the
  outputs, or the guide drops the step.

- [ ] **2026-11-11 | G20 | NEW | P2 | M - HTTPS installs.** The MSI creates an HTTP-only site, and
  the tray hardcodes `http://localhost:<port>` with redirects off, while DEPLOYMENT.md and
  SETUP-GUIDE.md recommend adding an HTTPS binding and the app calls `UseHttpsRedirection`.
  **Acceptance:** optional certificate and HTTPS binding in the MSI. The tray reads the scheme and
  port the MSI records. `/healthz` answers the tray on loopback without a redirect. Document it.
  - **2026-11-12 progress: HTTPS in the installer.**
    - Built: optional `CERT_THUMBPRINT` and `HTTPS_PORT` (default 443) MSI properties, and
      `CertThumbprint` / `HttpsPort` in Setup.exe, which also has the two fields on its Options page.
      `ValidateSettingsExecute` checks, before anything changes, that the thumbprint names a
      certificate in `LocalMachine\My` with a private key, valid dates and Server Authentication, and
      that the port is free and not the HTTP port; otherwise it stops with a message saying which.
      The new `ConfigureHttps` custom action adds the `https` binding with appcmd (the existing
      pattern) and binds the certificate in http.sys with netsh. Uninstall, or `CERT_THUMBPRINT=none`,
      removes both.
    - Recorded: `SiteScheme`, `HttpsPort`, `CertThumbprint` and `CertHost` under
      `HKLM\SOFTWARE\Architonix Labs\DBTracker`. Upgrades and repairs reuse them and check the
      certificate again, so a missing certificate stops the upgrade instead of quietly going back
      to HTTP (Angela).
    - Tray: polls `https://localhost:<HttpsPort>` when `SiteScheme` is `https`, accepting the
      certificate when it is in the machine's own `LocalMachine\My` (the name won't match
      `localhost`), and opens browser links at the certificate's name. Nothing recorded means
      `http://localhost:<SitePort>` as before (Sam).
    - Tests: thumbprint normalisation and validation, port choice and the commands in a new
      `tests/DBTracker.CustomActions.Tests` (32), and tray URL and TLS choice in the tray tests (62
      in total). `installer/build.ps1` and CI run both.
    - Docs: DEPLOYMENT.md has an HTTPS section; SETUP-GUIDE.md covers the Options fields.
    - Still open: a Hyper-V run with a real certificate (install, upgrade with and without a new
      thumbprint, `none`, uninstall), as part of G18.

- [ ] **2026-11-11 | G21 | NEW | P2 | M - Org quality and log standards.**
  - Add SonarQube analysis against `:7001`; tokens come from `scan.env`, never the repo.
  - Add a CI build and test pipeline; the repo has none.
  - Add SBOM upload to Dependency-Track.
  - Write logs to a folder that can be mounted at `/srv/weblogs/dbtracker/` for GoAccess (IIS W3C
    logs and app logs).

- [ ] **2026-11-11 | G22 | NEW | P3 | S - Verification records for I02 and I05.** The Progress log
  has no entry for `dotnet publish` with the profile or for the tray: autostart, the UAC actions and
  closing on uninstall. Run them and record the result.

- [x] **2026-11-11 | G23 | NEW | P2 | M - Bulk upload templates beyond the instance list.**
  The owner asked whether data can be uploaded from a filled-in template instead of entered by hand.
  Today only instances can be imported (`/instances/import`), and its template is a header row
  covering 19 columns. Sam and Priya need to onboard many databases at once. Priya also wants past
  backup and restore history loaded at onboarding. Angela needs imported evidence labelled as
  imported, with who and when, and never counted as verified.
  **Acceptance:**
  - The instance template has an example row and a second "allowed values" section (engines,
    roles, environments, criticality).
  - It adds the missing columns: Region, Cluster, ReplicaSet, Criticality, OwnerTeam,
    BackupContactName/Email/Phone, BackupFileNamePatterns, ConnectivityHost/Port and
    RestoreTestIntervalDays.
  - A backup and restore history import (instance name, time with zone, type, status, size,
    duration, notes) uses the same preview, error download and repeat-safe matching, and records
    entries with Source = Imported and the importing user.
  - Lifecycle evidence imports (catalogue, advisories, metadata, maintenance results) follow under
    G13 and G15.

### Epic 27 - Modern web platform (Chrome Modern Web Guidance review, 2026-11-11)

2026-11-11 | NEW-007 | The owner asked whether Chrome's Modern Web Guidance
(developer.chrome.com/docs/modern-web-guidance) can be used in DBTracker. Each of its 11 topics was
checked against the app and the personas. Adopted below.

Not adopted:
- Animated accordions: decorative motion the antislop pass removed on purpose.
- A sliding tab highlight: cosmetic, and Priya wants the console look kept.
- Hover preloading: Blazor Server navigates within one circuit, so speculative prerendering would
  make the server render pages nobody opens.

Browser target: Baseline "widely available". Newer features are used only as enhancements with a
fallback.

- [x] **2026-11-11 | G25 | NEW | P1 | S - Security headers and a Content Security Policy.**
  Angela and Sam: the app sends no CSP, frame, sniffing or referrer headers.
  **Acceptance:**
  - Send `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
    `frame-ancestors 'none'` and `Permissions-Policy` (camera allowed only where the QR flow needs it).
  - Ship a CSP in report-only mode first, then enforce it: `default-src 'self'`, `script-src
    'self'`, `connect-src 'self'` (covers the Blazor websocket), and `style-src 'self'
    'unsafe-inline'` until inline style attributes move to classes. `font-src` follows G27.
  - Verify the setup wizard, sign-in, the QR flow and every page with the E2E suite and no CSP
    console errors.

- [x] **2026-11-11 | G26 | NEW | P2 | M - Passkey sign-in.**
  Dev forgets passwords, Sam wants fewer resets, Angela wants phishing-resistant sign-in, and Noor
  signs in on a phone.
  **Acceptance:**
  - Use ASP.NET Core Identity's passkey support. Users register and remove passkeys on an account
    page, and sign in with one as well as with a password.
  - Registration and removal are audited, and deactivation (U16) blocks passkey sign-in.
  - A temporary-password user must still change it first.
  - The password stays as the fallback, and no passkey data is exported.
  - Verify the API and options against the current .NET 10 documentation before building.

- [x] **2026-11-11 | G27 | NEW | P2 | S - Faster first paint: self-hosted fonts, no unused script.**
  `bootstrap.bundle.min.js` (80 KB) loads on every page but nothing uses it, and fonts come from
  Google Fonts. Priya (LCP), Angela (no third-party request from an internal tool) and Sam (offline
  or locked-down servers render correctly) all gain.
  **Acceptance:**
  - Remove the unused script.
  - Serve Inter and IBM Plex Mono (both OFL) from `wwwroot` as WOFF2, with only the weights in use,
    preloaded, using `font-display: swap`.
  - Record LCP before and after on the dashboard (see G28).

- [x] **2026-11-11 | G28 | NEW | P2 | S - Core Web Vitals baseline (LCP, INP, CLS).**
  Measure before optimizing. This links to U06's never-run benchmark (G09).
  **Acceptance:**
  - Measure with Lighthouse or DevTools on the representative dataset: the dashboard, instance list,
    reports, instance detail and one form, on desktop and a mobile profile (Noor).
  - Record the numbers in the Progress log, list the long tasks and slow interactions found, and
    open items for anything outside "good".

- [x] **2026-11-11 | G29 | NEW | P3 | M - Native dialogs for confirmations.**
  Alex needs focus kept inside a confirmation, Esc to cancel, and focus returned afterwards. Noor
  needs it to fit a phone. Covers archive, cancel retirement checklist, revoke token and similar
  consequential actions.
  **Acceptance:**
  - Use `<dialog>` with `showModal()`, a labelled title, the preview text inside, and Esc or Cancel
    to close.
  - Existing data-testids keep working, and the E2E tests pass.

- [x] **2026-11-11 | G30 | NEW | P3 | M - Popover API for the notification menu and tooltips.**
  The notification dropdown is hand-built, and 21 controls rely on `title` tooltips that touch and
  keyboard users never see (Noor, Alex).
  **Acceptance:**
  - Use `popover` for the notification menu (light dismiss, top layer, focus return).
  - Replace `title`-only explanations with visible text or a toggletip using `popover`.
  - CSS anchor positioning only as an enhancement, with a plain fallback position.

- [x] **2026-11-11 | G31 | NEW | P3 | S - Container queries for cards and stat cells.**
  Noor and Alex at 200% zoom: dashboard stat cells and detail cards should reflow by their own
  width, not just the window's.
  **Acceptance:** container queries on the stat row, detail cards and key-value lists, with the
  existing media queries as fallback. Check at 360 px and at 200% zoom.

- [x] **2026-11-11 | G32 | NEW | P2 | M - Follow-ups found while closing G09-G15 and G23.**
  The four delivery tracks reported these gaps, which no earlier item covered.
  - **Monitoring and alerts:**
    - Webhook sends are not stored as delivery attempts (email is).
    - Opening, closing and escalating an incident by the alert worker is not audited (acknowledge
      and assign are).
    - `NotificationService` still loads every notification into memory.
    - A non-Admin who sends a report and gets an error is pointed to `/setup`, which only Admins
      can open.
  - **Time zones:** some times still use the server's zone, not the viewer's: scan times,
    maintenance, incidents, connectivity results, access tokens, the dashboard, notifications,
    lifecycle and campaign pages (the U10/U11 component `LocalTime` exists for this).
  - **Evidence:**
    - Manual backup and restore entries are not audited.
    - Restore-event corrections are ignored when deciding whether an instance is restore-tested.
  - **Lifecycle:** instances with no version identity are skipped by the support-runway sweep;
    they show as Unknown on the page but raise no notice.
  - **Assurance:**
    - The retirement gate doesn't count hosted databases (D15 added them).
    - The manual "Record result (now)" form for maintenance results can't backdate; imports can.
  - **Code:** `CsvRecordSplitter` (assurance imports) duplicates `CsvRecordReader`; merge them.
  - **Decision for the owner:** response-time targets for U06. The benchmark (1,000 instances,
    100,607 backups) gave medians of 48-65 ms for a list page and 144-240 ms for the heaviest
    report loads. Suggested targets: 250 ms for a list page and 500 ms for a report, at this size.

- [x] **2026-11-12 | G33 | NEW | P3 | S - Layout shift on instance detail (phone).**
  Found by G28. On the 360 px, 4x CPU profile, instance detail scores CLS 0.110, just over the 0.1 "good" limit (desktop
  0.008). Noor sees the page jump as later sections load.
  **Acceptance:** find the shifting element with the Layout Shift API or DevTools, reserve its space, and re-measure with
  `CoreWebVitalsBaseline` to below 0.1.

- [ ] **2026-11-12 | G34 | NEW | P3 | S - Follow-ups found while closing G32.**
  - Post-change evidence candidates (`ChangeValidationService`) and `ChangeEvidenceRules` still use the original restore
    outcome, ignoring corrections (Angela).
  - Capacity measurement times on the assurance page are entered and shown as plain UTC with no zone choice. The capacity
    table, saved capacity limit and forecast dates print UTC timestamps with no label (Priya, Noor).
  - Support-runway notice emails are not stored as delivery attempts (Sam).

- [x] **2026-11-12 | G35 | NEW | P1 | S - An installed app must survive a database that starts after it.**
  Found by the first Hyper-V upgrade run (G18). After the VM restarted, IIS started DBTracker while SQL Server Express was
  still `StartPending`. The one start-up migration failed, and the site answered HTTP 500 until someone recycled it.
  Sam needs it to recover by itself, Priya needs the app back after a reboot, and Angela needs no half-applied state.
  **Delivered:**
  - Start-up retries the migration four times with a short backoff.
  - If the database still doesn't answer, the site starts anyway and pages answer 503 with a plain "waiting for its
    database" explanation and `Retry-After: 15`. `/healthz` reports degraded, so the tray light turns amber.
  - `DatabaseReadinessService` retries every 15 seconds and opens the site once migrations are applied.
  - Development and Testing still fail loudly.
  - The E2E test `DatabaseUnavailableAtStartupTests` starts a Production-mode copy against a missing SQL Server and checks
    the 503, `Retry-After` and degraded health.
  - The first version gated on "not migrated yet", which held the sign-in page after a fresh install finished the wizard;
    the install E2E tests caught it. The gate now applies only while an installed app is actually waiting for its database.
  - Full E2E: 145 of 147, with the 2 wizard failures fixed and rerun green.
  - Verified in the Hyper-V upgrade rerun (1.1.0 to 1.1.1): the site answered after the upgrade and reboot.

### Audit corrections to existing text

- 2026-11-11 | UPDATE-017 | Clarifies the Epic 13-17 intro ("All items below are proposed and unimplemented"), the "planning checkboxes remain open" sentence, and the Epic 18-22 intro ("All D items below are proposed and unimplemented"): all are now ticked, with remaining gaps tracked as G09-G16. The original wording is kept as history.
- 2026-11-11 | UPDATE-018 | Tracking correction: UPDATE-015 was used twice. The 2026-11-10 entry (Epics 13 and 14 implemented) and the 2026-11-11 entry (Design theme superseded) are distinct; refer to them by date as well as reference.
- 2026-11-11 | UPDATE-019 | Supersedes the "Out of scope" line for a custom from/to date range on Backup Activity: it is built (Reports "Custom" range). The caption bug is in G11.
- 2026-11-11 | UPDATE-020 | Clarifies Epic 3's shared filter bar (see also UPDATE-002): the full filter bar is on Instances only; the dashboard filters by health and links to Instances. No change is planned unless a persona asks.
- 2026-11-11 | UPDATE-021 | Clarifies Epic 11: the Reports switcher also has a Trends report (U22), which is not in the Epic 11 list. Epic 12's "My instances" also matches the backup contact and team membership, and the maintenance icon is a cone, not a wrench.
- 2026-11-11 | UPDATE-022 | Clarifies the Progress log: the support-runway check is enabled by default (1440 minutes), not disabled. The D22 entry's "NotApplicable is not treated as passing" and "no second archive path" are contradicted by the code; see G01. Settings missing from the backlog: `BackupScan:StaleAfterMultiplier` (3) and `Rehearsal:DefaultValidityDays`.
- 2026-11-11 | UPDATE-023 | Clarifies the open Design "Further polish" item: the mobile pass is partly done (icon rail below 900px, 44px tap targets, stacked key-value rows), while the light-theme toggle and zero-state illustrations are not built. The item stays open.
- 2026-11-11 | UPDATE-024 | Documentation to correct with G16/G19: README project layout omits `src/DBTracker.Tray`, `src/DBTracker.Tray.Core`, `installer/` and `tests/DBTracker.Tray.Tests`; CHANGELOG 1.0.0 claims a tray site restart that does not exist.

Sequencing: Epic 24 first, starting with G01-G04 (small, and each makes a ticked claim true). Then
G17-G19 before the next release. G09-G16 follow by area, each closed only after its gaps are
tested, using the persona tasks in USABILITY_REVIEW.md and DBA_LIFECYCLE_REVIEW.md as earlier.
