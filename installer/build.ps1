<#
.SYNOPSIS
    Builds the DBTracker installer: publishes the web app, tests and builds the tray app, builds DBTracker.msi, then
    DBTracker-Setup.exe (the prerequisites bundle) when that project is present.

.EXAMPLE
    .\installer\build.ps1 -Version 1.2.0
    .\installer\build.ps1 -SkipBundle
    .\installer\build.ps1 -Validate        # ICE validation; run from an elevated prompt
#>
[CmdletBinding()]
param(
    [string]$Version = "1.1.0",
    [string]$Configuration = "Release",
    [switch]$SkipBundle,
    [switch]$Validate
)

$ErrorActionPreference = "Stop"
$installer = $PSScriptRoot
$root = Split-Path $installer -Parent
$out = Join-Path $installer "out"
$publish = Join-Path $out "publish"

function Invoke-Step([string]$Description, [scriptblock]$Command) {
    Write-Host "==> $Description" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Description failed (exit code $LASTEXITCODE)." }
}

if ($Validate) {
    $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "-Validate runs Windows Installer ICE validation, which needs an elevated prompt."
    }
}

New-Item -ItemType Directory -Force $out | Out-Null
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Invoke-Step "Publish DBTracker.Web $Version ($Configuration)" {
    dotnet publish (Join-Path $root "src\DBTracker.Web\DBTracker.Web.csproj") -c $Configuration -o $publish -p:Version=$Version --nologo
}

$leaked = Get-ChildItem $publish -Filter "appsettings.*.json" | Where-Object { $_.Name -match '^appsettings\.(Development|Testing)\.json$' }
if ($leaked) { throw "Publish output contains $($leaked.Name -join ', '); these must not ship." }

$tray = Join-Path $out "tray"
if (Test-Path $tray) { Remove-Item $tray -Recurse -Force }
Invoke-Step "Test the tray app logic" {
    dotnet test (Join-Path $root "tests\DBTracker.Tray.Tests\DBTracker.Tray.Tests.csproj") -c $Configuration --nologo
}
Invoke-Step "Build DBTracker Tray $Version ($Configuration)" {
    dotnet build (Join-Path $root "src\DBTracker.Tray\DBTracker.Tray.csproj") -c $Configuration -o $tray -p:Version=$Version -p:DebugType=none --nologo
}

$msiProject = Join-Path $installer "DBTracker.Msi\DBTracker.Msi.wixproj"
$msiBin = Join-Path $installer "DBTracker.Msi\bin"
Invoke-Step "Build DBTracker.msi" {
    dotnet build $msiProject -c $Configuration -p:ProductVersion=$Version -p:PublishDir="$publish\" -p:TrayDir="$tray\" -p:SuppressValidation=$(if ($Validate) { 'false' } else { 'true' }) --nologo
}
$msi = Get-ChildItem $msiBin -Recurse -Filter "DBTracker.msi" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msi) { throw "DBTracker.msi was not produced under $msiBin." }
$msiOut = Join-Path $out "DBTracker.msi"
Copy-Item $msi.FullName $msiOut -Force
Write-Host "    $msiOut"

$bundleProject = Join-Path $installer "DBTracker.Bundle\DBTracker.Bundle.wixproj"
if (-not $SkipBundle -and (Test-Path $bundleProject)) {
    # Always refresh: a new .NET patch changes the pinned download and its SHA-512.
    Invoke-Step "Refresh prerequisite metadata" {
        & (Join-Path $installer "DBTracker.Bundle\Get-PrereqMetadata.ps1")
    }
    Invoke-Step "Build DBTracker-Setup.exe" {
        dotnet build $bundleProject -c $Configuration -p:BundleVersion=$Version -p:DBTrackerMsiPath="$msiOut" --nologo
    }
    $setupOut = Join-Path $out "DBTracker-Setup.exe"
    if (-not (Test-Path $setupOut)) {
        $built = Get-ChildItem (Join-Path $installer "DBTracker.Bundle\bin") -Recurse -Filter "*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $built) { throw "The bundle build produced no .exe." }
        Copy-Item $built.FullName $setupOut -Force
    }
    Write-Host "    $setupOut"
}
elseif (-not $SkipBundle) {
    Write-Host "==> Skipping DBTracker-Setup.exe: installer\DBTracker.Bundle is not in this checkout." -ForegroundColor Yellow
}

# SETUP-GUIDE.md tells users to check their download against this file; publish it beside the installers.
# Format matches sha256sum: "<lowercase hash>  <file name>", so `sha256sum -c` also works.
Invoke-Step "Write SHA256SUMS.txt" {
    $sums = Join-Path $out "SHA256SUMS.txt"
    $lines = Get-ChildItem $out -File |
        Where-Object { $_.Extension -in '.exe', '.msi' } |
        Sort-Object Name |
        ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
    [System.IO.File]::WriteAllLines($sums, [string[]]$lines)
    Write-Host "    $sums"
}

Write-Host "Done. Output in $out" -ForegroundColor Green
