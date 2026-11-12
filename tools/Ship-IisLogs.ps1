<#
.SYNOPSIS
Copies DBTracker's IIS W3C logs to the central GoAccess server (/srv/weblogs/dbtracker on 192.168.1.8).

.DESCRIPTION
GoAccess runs once for every app on 192.168.1.8 and reads /srv/weblogs/<app>/. DBTracker runs under IIS on
Windows, so this script ships the site's W3C log files there with scp (OpenSSH, built into Windows). Only
files new or changed since the last run are sent; the last-run time is kept beside the script.

Run it from Task Scheduler (for example every 15 minutes) as an account that can read the IIS log folder
and has an SSH key accepted by the destination. No password is stored or prompted for.

.EXAMPLE
.\Ship-IisLogs.ps1 -WhatIf
.\Ship-IisLogs.ps1 -SiteName DBTracker -Destination ram@192.168.1.8:/srv/weblogs/dbtracker/
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SiteName = "DBTracker",
    [string]$Destination = "ram@192.168.1.8:/srv/weblogs/dbtracker/",
    [string]$StateFile = (Join-Path $PSScriptRoot "Ship-IisLogs.lastrun")
)

$ErrorActionPreference = "Stop"

# The site id names its log folder (W3SVC<id>); read it from IIS config so a reinstall on another id still works.
$appcmd = Join-Path $env:windir "System32\inetsrv\appcmd.exe"
if (-not (Test-Path $appcmd)) { throw "IIS isn't installed on this machine ($appcmd not found)." }
$siteXml = [xml](& $appcmd list site $SiteName /config /xml)
$site = $siteXml.appcmd.SITE
if (-not $site) { throw "IIS has no site named '$SiteName'." }
$siteId = $site.'SITE.ID'

$logRoot = [Environment]::ExpandEnvironmentVariables($site.site.logFile.directory)
if ([string]::IsNullOrWhiteSpace($logRoot)) { $logRoot = Join-Path $env:SystemDrive "inetpub\logs\LogFiles" }
$logDir = Join-Path $logRoot "W3SVC$siteId"
if (-not (Test-Path $logDir)) { throw "No log folder yet at $logDir. IIS writes it after the site's first request." }

$since = if (Test-Path $StateFile) { [datetime]::Parse((Get-Content $StateFile -Raw).Trim()).ToUniversalTime() } else { [datetime]::MinValue }
$startedUtc = [datetime]::UtcNow
$files = Get-ChildItem $logDir -Filter "*.log" | Where-Object { $_.LastWriteTimeUtc -gt $since } | Sort-Object Name

if ($files.Count -eq 0) {
    Write-Host "No new or changed log files in $logDir since $since (UTC)."
    return
}

foreach ($file in $files) {
    if ($PSCmdlet.ShouldProcess("$($file.FullName) -> $Destination", "scp")) {
        & scp -q -o BatchMode=yes $file.FullName $Destination
        if ($LASTEXITCODE -ne 0) { throw "scp failed for $($file.Name) (exit code $LASTEXITCODE). Nothing after it was sent." }
        Write-Host "Sent $($file.Name)"
    }
}

if (-not $WhatIfPreference) {
    Set-Content -Path $StateFile -Value $startedUtc.ToString("o") -Encoding ascii
}
