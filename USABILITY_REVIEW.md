# DBTracker usability and feature review

Review date: 2026-11-10. Scope: repository-based product review of inventory, dashboard,
instance editing, backup/restore entry, scanning, alerts, reporting, account flows, domain logic,
data access, styling, and existing test coverage. Implementation proposals are in
[BACKLOG.md](BACKLOG.md#usability-roadmap--2026-11-10-review).

This is a structured persona assessment, not interviews or observed user testing. Findings below
come from source inspection; priorities, effort, and success targets are estimates. No live UI,
production data, external services, or performance measurements were used. Existing test sources
were reviewed; the historical test counts in the backlog were not rerun or independently verified.

## Product assessment

DBTracker has a useful operational core: heterogeneous inventory, risk-first dashboard, manual
and discovered backup records, restore history, role-based navigation, imports, maintenance,
notifications, and several reports. Pure report/health builders and existing unit/E2E tests offer
a useful foundation for extending it. The next investment should make everyday decisions faster
and more reliable, rather than add more top-level screens or another visual redesign.

Four recurring weaknesses emerged:

1. **Confidence in status.** A file is treated as a successful full backup without verification;
   scan success has no age-based expiry; open pages do not refresh their data automatically.
2. **Completing a task.** Users can identify a problem but cannot acknowledge/assign it, jump from
   a notification to its instance, recover an archived record through the UI, or retain a useful
   filtered view in a URL.
3. **Evidence and context.** Reports describe current state, restore coverage means any historical
   success, and manual event timestamps always use entry time. Those limitations need explicit
   language before richer claims are made.
4. **Safe, inclusive operation.** Wide tables and mouse-only row navigation impede some users;
   forms lack consistent validation, protection from losing edits, and conflict handling.

## Personas and decision rules

| Persona | Job and success condition | Feature constraint |
|---|---|---|
| Priya, DBA | Triage a failed production backup and record the actual event quickly | Show reasons and useful actions; avoid extra mandatory fields on normal entry |
| Marcus, IT manager | Find the responsible team and explain outstanding risk | Prioritize ownership, trends, and explicit report scope over more raw tables |
| Angela, auditor | Reproduce what was known and distinguish evidence from assertions | Preserve provenance, revisions, timestamps, and policy context; never imply a file proves recoverability |
| Dev, application owner | Check owned applications and reach the right person | Default to a concise view; a personal filter must not be presented as an access restriction |
| Noor, on-call responder using a phone | Find and hand off an incident away from a workstation | Key actions must work on narrow screens and survive navigation/reconnection clearly |
| Sam, administrator | Onboard/offboard users and diagnose monitoring configuration | Expose actionable status without revealing credentials or requiring database edits |
| Alex, keyboard/screen-reader user | Complete the same tasks with predictable focus and readable feedback | Semantic links, named inputs, accessible errors, and status text are baseline usability |

These are analytical roles, including three additions to the original four personas. Agreement
means a proposal has a concrete beneficiary and that other personas' concerns have a bounded
resolution; it does not mean every persona needs every feature.

## Findings grounded in the implementation

Paths are relative to the repository root. Referenced symbols are useful search targets.

| Evidence | Current behavior and user consequence | Roadmap |
|---|---|---|
| `src/DBTracker.Infrastructure/Scanning/BackupFileScanner.cs`, `ScanAsync` | Every top-level file is assigned Full/Success, irrespective of extension, completeness, or validity; matching only by name ignores replacement files | U01 |
| `src/DBTracker.Web/Components/Pages/Home.razor`, `OnInitializedAsync`; `ScanActivityReportBuilder.cs` | Banner counts Error/NeverScanned; an old Success is not classified stale | U02 |
| `Home.razor`, `InstancesList.razor`, `Reports.razor` initialization | Overview data loads on initialization; no refresh control/timer in these pages | U03 |
| `src/DBTracker.Core/Services/BackupHealthCalculator.cs`, `Compute` | Latest record determines status with one interval across all backup types; future records are not excluded by `asOfUtc` | U04, U10, U11 |
| `InstancesList.razor`, `Filtered`; `Home.razor`, `Sorted` | Inventory is name-sorted with local filter fields; no column sort controls, dedicated department/owner filters, or persisted query state; dashboard only filters health | U05 |
| `src/DBTracker.Infrastructure/Queries/InstanceOverviewService.cs`, `GetAllAsync` | Loads all matching instances and their entire backup/restore histories; tables render unbounded lists | U06 |
| `NotificationService.cs`, `MarkAllReadAsync`; `NotificationBell.razor` | One shared read flag, global mark-all-read, latest 20 entries, no instance navigation | U07 |
| `AlertCheckHostedService.cs`; `src/DBTracker.Core/Services/AlertEvaluator.cs` | State-change Overdue/Failed alerts only; no acknowledgement, assignment, reminder, recovery notification, or NeverBackedUp alert | U08 |
| `ComplianceReportService.cs`, `SendNowAsync`; `Compliance.razor`, `SendReport` | Send reloads full current inventory regardless of page filters; UI does not preview recipients/scope | U09 |
| `NullEmailSender.cs`, `SmtpEmailSender.cs`; `HttpWebhookSender.cs` | Disabled/incomplete mail can complete without sending; report service returns recipient count; webhook failures only log | U09, U15 |
| `Compliance.razor`; `RestoreTestingReportBuilder.cs` | Compliance is current-load state; restore tested means any successful restore, regardless of age or backup used | U10, U12 |
| `InstanceDetail.razor`, `Load`, `LogBackup`, `LogRestore` | Manual timestamps reset to now on submit; notes and source file are not shown in history tables | U11 |
| `InstanceEditor.razor`, `Save`; `DatabaseInstance.cs`; `InstancesImport.razor` | No consistent length/range validation, dirty-form guard, conflict version, or failed-save recovery; import directly adds parsed rows | U13, U17 |
| `InstanceDetail.razor`, `Archive`; `InstanceOverviewService.cs` | Archive immediately hides an instance from normal inventory; includeArchived exists in service but no archive browser/unarchive UI | U14 |
| `Users.razor`; `Account/Pages/Login.razor` | User UI creates accounts only; no exposed password-change/reset flow | U16 |
| `InstanceCsvImporter.cs`, `Parse` | Line-based parsing cannot handle quoted multiline fields; imports do not compare with existing inventory or validate all form constraints | U17 |
| `InstanceDetail.razor`, `TestConnection`; `DefaultPortMapper.cs` | Connectivity uses conventional port only; custom endpoints cannot be tested correctly | U18 |
| `InstanceChangeLogBuilder.cs`; detail mutation handlers | Form edits are diffed; archive and maintenance do not write that trail; manual backup creator is not stored | U19 |
| `InstancesList.razor`, `Home.razor`; `InstanceEditor.razor`; `ToastHost.razor`; `wwwroot/app.css` | Navigation relies on row clicks; many labels lack input association; toasts lack a live region. Horizontal overflow and reduced-motion support already exist | U20 |
| `DatabaseInstance.cs`; `InstancesList.razor`, `_myEmail` | Ownership is one free-text contact; My instances compares that email; tags exist but lack structured facets | U21 |
| `Reports.razor`; `BackupActivityReportBuilder.cs`; `OwnershipRollupBuilder.cs` | Current totals and run lists exist; no stored period comparisons or overdue-duration trends | U22 |

## Persona challenge and resolution

| Proposal family | Sponsor | Challenge from another persona | Agreed scope |
|---|---|---|---|
| Evidence confidence and freshness (U01-U04) | Angela, Priya | Dev: more warning badges could overwhelm a simple check | Plain-language reason and next due time first; detailed provenance in drill-down; keep backup and scanner health separate |
| Saved views and scalable lists (U05-U06) | Priya, Marcus | Alex: dense configurable grids can become hard to operate | Start with useful defaults, real links and sort controls; preserve filters and keyboard position |
| Notifications and incident work (U07-U08) | Noor, Priya | Angela: acknowledgement must not make failed backups look healthy | Personal read state and separate shared incident state; resolve only from new evidence; audit all actions |
| Report scope and historical evidence (U09-U10) | Marcus, Angela | Sam: recreating old inventory and policy is a larger data change | Correct send scope/status first; add immutable generated snapshots before attempting arbitrary historical reconstruction |
| Event entry and restore assurance (U11-U12) | Priya, Angela | Priya: routine logging must remain fast | Default to now; advanced evidence fields optional unless policy requires them; record entry time separately |
| Forms, archive, and account recovery (U13-U16) | Sam, Dev | Noor: confirmations on every action slow triage | Guard unsaved edits, conflicts, and consequential archive actions; routine logging gets immediate feedback |
| Imports, endpoints, audit, ownership (U17-U19, U21) | Sam, Marcus | Dev: owner self-service could accidentally grant database editing rights | Review changes before applying; explicit identity/role boundaries; contact correction requests first |
| Accessible mobile use (U20) | Alex, Noor | Priya: replacing the console theme would disrupt familiar workflows | Keep the theme; improve semantics, reflow, focus, contrast checks, and readable feedback |
| Trends (U22) | Marcus | Angela: totals could imply compliance without complete inventory or historical policy | Publish denominator, scope, and missing-evidence counts; defer claims until snapshots are available |

## Recommended delivery and validation

Use P1 for the next delivery candidates, P2 for the following increments, and P3 for later
enhancement. This is a proposed sequence, not a committed schedule. S/M/L are relative estimates
(small/local, medium/cross-layer, large/new persistent model or historical semantics).

1. **Trust and basic task completion:** U01-U04, U07, U09, U13, U14, U20. These remove misleading
   status, cross-user interference, lost work, and navigation barriers.
2. **Daily operational efficiency:** U05-U06, U08, U11-U12, U15-U18. U16 remains P1 for deployments
   that need onboarding/offboarding immediately; tracks can proceed independently.
3. **Evidence and organizational maturity:** U10, U19, U21, U22. Capture needed history early;
   historical views depend on retained snapshots, policy versions, and reliable event times.

Proposed acceptance exercises, to run after the corresponding implementation:

- Priya finds a failed production instance, sees the reason/deadline, opens its runbook, and logs
  an event without leaving the workflow. Target: routine backup entry within 10 seconds, retaining
  the original persona goal; measure after setup, without evidence attachments.
- Noor opens a notification at a 360px viewport, reaches the correct instance, acknowledges it,
  and hands off to another responder. No hidden primary action or horizontal page overflow.
- Angela exports a fixed snapshot, adds a later backup, then reopens the snapshot. Values and
  scope remain identical; the export identifies event time, generation time, and evidence source.
- Dev saves a personal application view and reopens its URL after sign-in. Filters survive;
  the UI clearly distinguishes personal selection from authorization.
- Sam imports a file containing duplicates, bad values, multiline notes, and one valid change.
  Preview matches applied changes; errors are downloadable; repeating the import adds no duplicates.
- Alex performs inventory navigation, filtering, event entry, validation recovery, and report
  export using keyboard and assistive technology. Focus and status changes remain understandable.
- Test U06 with a representative agreed dataset (suggested starting fixture: 1,000 instances and
  100,000 backup records). Record a baseline before selecting response-time targets; no speed
  improvement is claimed by this review.

## Deliberately deferred

- Backup execution, automated restore execution, and enterprise SSO remain outside current scope.
- An AI chat assistant, native mobile app, topology canvas, and another theme redesign have weaker
  evidence than fixing the existing workflows. Responsive web improvements serve the mobile task.
- Light mode remains an optional preference, not a substitute for readable contrast and semantics.
- Scheduled delivery of every report remains deferred until scope and delivery status are reliable.
- Arbitrary past-date reconstruction cannot be promised from current rows alone. Generated snapshots
  are the first deliverable; backdated evidence must not silently rewrite historical exports.
- The repository has no visible SonarQube configuration or deployment/quality pipeline in the file
  inventory reviewed. Shared quality-service integration is an engineering follow-up under the
  parent `AGENTS.md`, separate from this usability roadmap; no infrastructure change was made.

## Backlog corrections

Epic 1 described the inventory as sortable, but its headers currently have no sort controls.
Epic 3 described a shared combined dashboard/inventory filter bar, but those pages have distinct
and unequal controls. Epic 12's scan indicator is delivered for Error/NeverScanned, not elapsed-time
staleness. Those claims are clarified in the backlog, with new work remaining unchecked.
