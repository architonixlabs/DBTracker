# Launches a clean Windows Sandbox that installs DBTracker-Setup.exe from scratch (IIS, Hosting
# Bundle, SQL Server Express, the MSI) and writes its findings to installer\out\sandbox-results.
# Requires the Windows Sandbox feature (Containers-DisposableClientVM).
param([switch]$Rebuild)

$ErrorActionPreference = 'Stop'
$installerDir = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $installerDir 'out'
$setup = Join-Path $outDir 'DBTracker-Setup.exe'

if (-not (Test-Path "$env:windir\System32\WindowsSandbox.exe")) {
    throw 'Windows Sandbox is not enabled. Run (as admin): Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All, then reboot.'
}

if ($Rebuild -or -not (Test-Path $setup)) {
    & (Join-Path $installerDir 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
}

$results = Join-Path $outDir 'sandbox-results'
if (Test-Path $results) { Remove-Item $results -Recurse -Force }
New-Item -ItemType Directory -Path $results | Out-Null

$wsb = Join-Path $outDir 'DBTracker-Sandbox.wsb'
@"
<Configuration>
  <Networking>Enable</Networking>
  <MemoryInMB>8192</MemoryInMB>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$outDir</HostFolder>
      <SandboxFolder>C:\Installer</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$PSScriptRoot</HostFolder>
      <SandboxFolder>C:\Test</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$results</HostFolder>
      <SandboxFolder>C:\Results</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Test\Test-InSandbox.ps1</Command>
  </LogonCommand>
</Configuration>
"@ | Set-Content -Path $wsb -Encoding UTF8

Write-Host "Starting Windows Sandbox. Results will appear in $results\results.json"
Start-Process $wsb
