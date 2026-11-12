#Requires -RunAsAdministrator
# Upgrade test (G18): the Start-HyperVTest.ps1 VM is left with the previous version installed by
# Test-HyperVUninstall.ps1's last step. This records the installed version and App_Data's setup key,
# runs the current installer\out\DBTracker-Setup.exe over it (an in-place major upgrade), checks the
# version moved forward and App_Data was kept, then runs the uninstall test against the new version.
# Results land in installer\out\hyperv-upgrade\upgrade-results.json.
param(
    [Parameter(Mandatory)][string]$VhdPath,
    [string]$VmName = 'DBTracker-Test',
    [int]$Port = 8150
)

$ErrorActionPreference = 'Stop'
$outDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'out'
$resultsDir = Join-Path $outDir 'hyperv-upgrade'
$loginFile = Join-Path $outDir 'hyperv-results\vm-login.json'
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
Start-Transcript -Path (Join-Path $resultsDir 'transcript.txt') -Force | Out-Null
function Step($text) { Write-Host "[$(Get-Date -Format HH:mm:ss)] STEP: $text" }

function Get-InstalledState($cred) {
    $session = New-PSSession -VMName $VmName -Credential $cred
    try {
        Invoke-Command -Session $session -ArgumentList $Port -ScriptBlock {
            param($port)
            $entry = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
                Where-Object { $_.DisplayName -like 'DBTracker*' } | Sort-Object DisplayVersion -Descending | Select-Object -First 1
            $key = 'C:\inetpub\DBTracker\App_Data\setup-key.txt'
            $config = 'C:\inetpub\DBTracker\App_Data\dbtracker.config.json'
            $health = try { (Invoke-WebRequest "http://localhost:$port/healthz" -UseBasicParsing -TimeoutSec 30).StatusCode } catch { 0 }
            [pscustomobject]@{
                DisplayName = $entry.DisplayName
                DisplayVersion = $entry.DisplayVersion
                SetupKeyHash = if (Test-Path $key) { (Get-FileHash $key -Algorithm SHA256).Hash } else { $null }
                ConfigHash = if (Test-Path $config) { (Get-FileHash $config -Algorithm SHA256).Hash } else { $null }
                HealthzStatus = $health
            } | ConvertTo-Json
        } | ConvertFrom-Json
    } finally {
        Remove-PSSession $session
    }
}

$summary = [ordered]@{ Started = (Get-Date).ToString('o') }
try {
    if (-not (Test-Path $loginFile)) { throw "No ${loginFile}: run Start-HyperVTest.ps1 once to create the VM." }
    $password = (Get-Content $loginFile -Raw | ConvertFrom-Json).Password
    $cred = New-Object PSCredential('Administrator', (ConvertTo-SecureString $password -AsPlainText -Force))
    if ((Get-VM -Name $VmName).State -ne 'Running') { Start-VM -Name $VmName; Start-Sleep -Seconds 60 }

    Step 'Recording the installed version and App_Data before the upgrade'
    $before = Get-InstalledState $cred
    $summary.Before = $before
    if (-not $before.DisplayVersion) { throw 'DBTracker is not installed in the VM, so there is nothing to upgrade. Run Test-HyperVUninstall.ps1 first; its last step leaves it installed.' }

    Step "Upgrading $($before.DisplayVersion) with the current installer (Start-HyperVTest.ps1 -ReuseVm)"
    & (Join-Path $PSScriptRoot 'Start-HyperVTest.ps1') -VhdPath $VhdPath -VmName $VmName -Port $Port -ReuseVm
    $summary.InstallResults = Get-Content (Join-Path $outDir 'hyperv-results\results.json') -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json

    Step 'Recording the installed version and App_Data after the upgrade'
    $after = Get-InstalledState $cred
    $summary.After = $after
    $summary.VersionMovedForward = [version]$after.DisplayVersion -gt [version]$before.DisplayVersion
    $summary.AppDataKept = ($before.SetupKeyHash -eq $after.SetupKeyHash) -and ($before.ConfigHash -eq $after.ConfigHash)
    $summary.SiteAnswers = $after.HealthzStatus -eq 200
    $summary.Passed = $summary.VersionMovedForward -and $summary.AppDataKept -and $summary.SiteAnswers

    Step 'Running the uninstall test against the new version'
    & (Join-Path $PSScriptRoot 'Test-HyperVUninstall.ps1') -VmName $VmName -Port $Port
} catch {
    Write-Host "FAILED: $($_.Exception.Message)"
    $summary.HostError = $_.Exception.Message
    $summary.Passed = $false
} finally {
    $summary.Finished = (Get-Date).ToString('o')
    $summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $resultsDir 'upgrade-results.json')
    Step "Done. Passed: $($summary.Passed). Results in $resultsDir"
    Stop-Transcript | Out-Null
}
