# DBTracker

A .NET Core Blazor app for tracking database backups across a heterogeneous
environment: SQL Server, MySQL, PostgreSQL, MongoDB, Oracle and more. Tracks
which application/company/department owns each instance, contact info,
active/passive role, server and database IPs, and whether backups are
actually happening on schedule.

## Install
Download **DBTracker-Setup.exe** from [Releases](https://github.com/architonixlabs/DBTracker/releases)
and follow the [setup guide](SETUP-GUIDE.md). What changed in each version: [CHANGELOG.md](CHANGELOG.md).

## Stack
- .NET 10, Blazor Web App (interactive server)
- EF Core 10 + SQL Server
- Bootstrap 5 (layout/forms) + a custom dark ops-console theme
- xUnit (unit tests), Playwright + NUnit (end-to-end tests)

## Running locally
Requires a local SQL Server instance reachable with Windows Authentication.

```bash
dotnet ef database update --project src/DBTracker.Infrastructure --startup-project src/DBTracker.Web
dotnet run --project src/DBTracker.Web
```

For local development the connection string lives in
`src/DBTracker.Web/appsettings.Development.json` (`ConnectionStrings:DBTracker`) and defaults to
`Server=localhost` with integrated security, so no secrets are required. Deployed (Production)
instances get theirs from the `/install` setup wizard instead; see [DEPLOYMENT.md](DEPLOYMENT.md).

## Deploying to IIS
See [DEPLOYMENT.md](DEPLOYMENT.md). A first-run wizard at `/install` creates the database and the
first Admin account. `installer\build.ps1` builds `DBTracker-Setup.exe` (installs prerequisites, then
the app) and `DBTracker.msi` (the app only) into `installer\out`.

## Project layout
- `src/DBTracker.Core` — domain entities and backup-health logic
- `src/DBTracker.Infrastructure` — EF Core DbContext, migrations, queries
- `src/DBTracker.Web` — Blazor UI, the `/install` setup wizard and the tray API
- `src/DBTracker.Tray` — DBTracker Tray, the Windows notification-area app (.NET Framework 4.6.2)
- `src/DBTracker.Tray.Core` — the tray's testable logic (netstandard2.0)
- `installer/` — `DBTracker.Msi` (WiX 5), `DBTracker.Bundle` (Setup.exe), MSI custom actions,
  `build.ps1` and the Sandbox / Hyper-V install tests under `test/`
- `tests/DBTracker.UnitTests` — unit tests
- `tests/DBTracker.Tray.Tests` — tray logic tests
- `tests/DBTracker.E2E` — Playwright end-to-end tests

## Quality checks
- **On every push:** GitHub Actions (`.github/workflows/ci.yml`) builds the solution, runs the unit and
  tray tests and produces a CycloneDX SBOM as a build artifact.
- **Locally, before a merge:** run the E2E suite (`dotnet test tests/DBTracker.E2E`, needs SQL Server on
  localhost with Windows authentication; set `E2E_HEADED=1` to watch the browser).
- **Code platform:** run `bash tools/scan.sh` from Git Bash with Docker running. It scans the repo with
  the shared Architonix code platform (SonarQube :7001, Dependency-Track :7002/:7003, DefectDojo :7004
  on 192.168.1.8), using `DockerSecurity/code-platform/scan-project.sh` and its `scan.env`. No tokens
  live in this repo.

See [BACKLOG.md](BACKLOG.md) for the persona-driven product backlog.

## License
[MIT](LICENSE) — free to use, modify and distribute.
