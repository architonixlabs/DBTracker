# Runs inside Windows Sandbox (started by Start-SandboxTest.ps1). Installs DBTracker the way a fresh
# server would, records what happened in C:\Results (mapped back to installer\out\sandbox-results on
# the host), then opens the setup wizard and the setup key for a manual walk-through.
param(
    [string]$Installer = 'C:\Installer\DBTracker-Setup.exe',
    [string]$Results = 'C:\Results',
    [int]$Port = 8150
)

$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $Results | Out-Null
Start-Transcript -Path (Join-Path $Results 'transcript.txt') -Force | Out-Null

$summary = [ordered]@{ StartedAt = (Get-Date).ToString('o') }

Write-Host "Running $Installer quietly with SQL Server Express..."
$setupLog = Join-Path $Results 'DBTracker-Setup.log'
$proc = Start-Process -FilePath $Installer -Wait -PassThru -ArgumentList @(
    '/quiet', '/norestart', '/log', "`"$setupLog`"",
    'InstallSqlExpress=1', 'LaunchWizard=0', "SitePort=$Port")
$summary.SetupExitCode = $proc.ExitCode
$summary.FinishedAt = (Get-Date).ToString('o')

$components = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\InetStp\Components' -ErrorAction SilentlyContinue
$summary.IisInstalled = [bool](Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\InetStp' -ErrorAction SilentlyContinue)
$summary.IisWebSockets = $components.WebSockets -eq 1
$summary.AspNetCoreModule = Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
$summary.SqlExpressInstance = [bool](Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL' -ErrorAction SilentlyContinue).SQLEXPRESS

Import-Module WebAdministration -ErrorAction SilentlyContinue
$site = Get-Website -Name 'DBTracker' -ErrorAction SilentlyContinue
$summary.SiteState = if ($site) { $site.State } else { 'missing' }
$pool = Get-Item 'IIS:\AppPools\DBTracker' -ErrorAction SilentlyContinue
$summary.AppPoolManagedRuntime = if ($pool) { "'$($pool.managedRuntimeVersion)'" } else { 'missing' }

try {
    $resp = Invoke-WebRequest -Uri "http://localhost:$Port/install" -UseBasicParsing -TimeoutSec 120
    $summary.InstallPageStatus = $resp.StatusCode
} catch {
    $summary.InstallPageStatus = "error: $($_.Exception.Message)"
}

$keyFile = 'C:\inetpub\DBTracker\App_Data\setup-key.txt'
$summary.SetupKeyFileCreated = Test-Path $keyFile

# Burn writes one log per package next to the bundle log in $Results; the helper logs to %TEMP%.
Copy-Item (Join-Path $env:TEMP 'DBTracker-SetupHelper.log') $Results -ErrorAction SilentlyContinue

$summary | ConvertTo-Json | Set-Content -Path (Join-Path $Results 'results.json') -Encoding UTF8
$summary | Format-List | Out-String | Write-Host
Stop-Transcript | Out-Null

if ($summary.SetupKeyFileCreated) { Start-Process notepad.exe $keyFile }
Start-Process "http://localhost:$Port/install"
