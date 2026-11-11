<#
.SYNOPSIS
    Pins the prerequisite downloads for DBTracker-Setup.exe and writes PrereqMetadata.generated.wxi.

.DESCRIPTION
    .NET Hosting Bundle: URL and SHA-512 come from Microsoft's official .NET release metadata
    (releases-index.json -> <channel>/releases.json); the size comes from a HEAD request.
    SQL Server Express: Microsoft publishes no hash, so the bootstrapper is downloaded, its Authenticode
    signature must be valid and from Microsoft Corporation, and its size and SHA-512 are recorded.
    Every URL and every redirect hop must be on a Microsoft-owned host, or the script stops.
    Burn refuses any payload whose size or hash differs from what is written here.

.PARAMETER VerifyHostingBundle
    Also download the Hosting Bundle (about 115 MB) and check its SHA-512 and Microsoft signature.
#>
[CmdletBinding()]
param(
    [string]$Channel = '10.0',
    [string]$OutFile,
    [string]$SqlExpressLink = 'https://go.microsoft.com/fwlink/p/?linkid=2216019&clcid=0x409&culture=en-us&country=us',
    [switch]$VerifyHostingBundle
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults.
if (-not $OutFile) { $OutFile = Join-Path $PSScriptRoot 'PrereqMetadata.generated.wxi' }
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$ReleasesIndexUrl = 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json'

# Exact hosts, plus any *.microsoft.com. aka.ms is Microsoft's link shortener (the SQL Express fwlink
# redirects through it). dotnetcli.blob.core.windows.net is listed by name only: blob.core.windows.net
# as a whole is open to any Azure customer.
$AllowedExactHosts = @('aka.ms', 'dotnetcli.blob.core.windows.net')

function Assert-MicrosoftHost([Uri]$Uri) {
    if ($Uri.Scheme -ne 'https') { throw "Refusing non-HTTPS URL: $Uri" }
    $h = $Uri.Host.ToLowerInvariant()
    if ($AllowedExactHosts -contains $h) { return }
    if ($h -eq 'microsoft.com' -or $h.EndsWith('.microsoft.com')) { return }
    throw "Refusing URL on a host that is not Microsoft-owned: $Uri"
}

# Follows redirects one hop at a time so every hop is checked, not just the last one.
function Resolve-MicrosoftUrl([string]$Url) {
    $current = [Uri]$Url
    for ($hop = 0; $hop -lt 10; $hop++) {
        Assert-MicrosoftHost $current
        $req = [Net.HttpWebRequest]::Create($current)
        $req.Method = 'HEAD'
        $req.AllowAutoRedirect = $false
        $req.UserAgent = 'DBTracker-Setup-Build'
        try {
            $resp = $req.GetResponse()
        } catch [Net.WebException] {
            if ($_.Exception.Response) { $resp = $_.Exception.Response } else { throw }
        }
        try {
            $status = [int]$resp.StatusCode
            if ($status -ge 300 -and $status -lt 400) {
                $location = $resp.Headers['Location']
                if (-not $location) { throw "Redirect from $current has no Location header." }
                $current = New-Object Uri($current, $location)
                continue
            }
            if ($status -ne 200) { throw "HEAD $current returned HTTP $status." }
            return [pscustomobject]@{ Url = $current.AbsoluteUri; Size = [int64]$resp.ContentLength }
        } finally {
            $resp.Close()
        }
    }
    throw "Too many redirects resolving $Url."
}

function Assert-MicrosoftSignature([string]$Path) {
    $sig = Get-AuthenticodeSignature -FilePath $Path
    if ($sig.Status -ne 'Valid') { throw "Authenticode signature on $(Split-Path $Path -Leaf) is $($sig.Status)." }
    if ($sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "$(Split-Path $Path -Leaf) is signed by '$($sig.SignerCertificate.Subject)', not Microsoft Corporation."
    }
}

function Save-Download([string]$Url, [string]$Path) {
    Assert-MicrosoftHost ([Uri]$Url)
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $Url -OutFile $Path -UseBasicParsing -UserAgent 'DBTracker-Setup-Build'
}

function Escape-Xml([string]$Value) { [Security.SecurityElement]::Escape($Value) }

$work = Join-Path ([IO.Path]::GetTempPath()) ("dbtracker-prereqs-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

try {
    # .NET Hosting Bundle
    Assert-MicrosoftHost ([Uri]$ReleasesIndexUrl)
    $index = Invoke-RestMethod -Uri $ReleasesIndexUrl -UseBasicParsing
    $channelEntry = $index.'releases-index' | Where-Object { $_.'channel-version' -eq $Channel }
    if (-not $channelEntry) { throw "Channel $Channel not found in $ReleasesIndexUrl." }

    $releasesUrl = $channelEntry.'releases.json'
    Assert-MicrosoftHost ([Uri]$releasesUrl)
    $releases = Invoke-RestMethod -Uri $releasesUrl -UseBasicParsing
    $latest = $releases.releases | Where-Object { $_.'release-version' -eq $channelEntry.'latest-release' } | Select-Object -First 1
    if (-not $latest) { throw "Release $($channelEntry.'latest-release') not found in $releasesUrl." }

    $aspnet = $latest.'aspnetcore-runtime'
    $hostingFile = $aspnet.files | Where-Object { $_.name -eq 'dotnet-hosting-win.exe' } | Select-Object -First 1
    if (-not $hostingFile) { throw "No dotnet-hosting-win.exe in the $($aspnet.version) metadata." }
    if ($hostingFile.hash -notmatch '^[0-9a-fA-F]{128}$') { throw "Hosting Bundle hash in the metadata is not a SHA-512." }

    $ancmVersion = @($aspnet.'version-aspnetcoremodule')[0]
    if (-not $ancmVersion) { throw "No version-aspnetcoremodule in the $($aspnet.version) metadata." }

    $hosting = Resolve-MicrosoftUrl $hostingFile.url
    if ($hosting.Size -le 0) { throw "Could not read the Hosting Bundle size from $($hosting.Url)." }
    $hostingName = Split-Path ([Uri]$hosting.Url).AbsolutePath -Leaf
    $hostingHash = $hostingFile.hash.ToUpperInvariant()

    if ($VerifyHostingBundle) {
        $hbPath = Join-Path $work $hostingName
        Write-Host "Downloading $hostingName to verify it..."
        Save-Download $hosting.Url $hbPath
        $actual = (Get-FileHash -Path $hbPath -Algorithm SHA512).Hash
        if ($actual -ne $hostingHash) { throw "Hosting Bundle SHA-512 does not match Microsoft's metadata." }
        if ((Get-Item $hbPath).Length -ne $hosting.Size) { throw "Hosting Bundle size does not match the HEAD response." }
        Assert-MicrosoftSignature $hbPath
        Write-Host "Hosting Bundle hash and signature verified."
    }

    # SQL Server Express bootstrapper
    $sql = Resolve-MicrosoftUrl $SqlExpressLink
    $sqlName = Split-Path ([Uri]$sql.Url).AbsolutePath -Leaf
    $sqlPath = Join-Path $work $sqlName
    Write-Host "Downloading $sqlName to verify its signature..."
    Save-Download $sql.Url $sqlPath
    Assert-MicrosoftSignature $sqlPath
    $sqlItem = Get-Item $sqlPath
    $sqlHash = (Get-FileHash -Path $sqlPath -Algorithm SHA512).Hash.ToUpperInvariant()
    $sqlVersion = $sqlItem.VersionInfo.FileVersion
    if (-not $sqlVersion) { $sqlVersion = '1.0.0' }
    $sqlProduct = $sqlItem.VersionInfo.ProductName
    if (-not $sqlProduct) { $sqlProduct = 'Microsoft SQL Server Express' }

    $generated = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $content = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by Get-PrereqMetadata.ps1 at $generated. Do not edit; rerun the script instead. -->
<Include>
  <?define DotNetChannel = "$(Escape-Xml $Channel)" ?>
  <?define AspNetCoreRuntimeVersion = "$(Escape-Xml $aspnet.version)" ?>
  <?define AncmMinVersion = "$(Escape-Xml $ancmVersion)" ?>
  <?define HostingBundleFileName = "$(Escape-Xml $hostingName)" ?>
  <?define HostingBundleUrl = "$(Escape-Xml $hosting.Url)" ?>
  <?define HostingBundleSize = "$($hosting.Size)" ?>
  <?define HostingBundleSha512 = "$hostingHash" ?>
  <?define SqlExpressFileName = "$(Escape-Xml $sqlName)" ?>
  <?define SqlExpressUrl = "$(Escape-Xml $sql.Url)" ?>
  <?define SqlExpressSize = "$($sqlItem.Length)" ?>
  <?define SqlExpressSha512 = "$sqlHash" ?>
  <?define SqlExpressVersion = "$(Escape-Xml $sqlVersion)" ?>
  <?define SqlExpressProductName = "$(Escape-Xml $sqlProduct)" ?>
</Include>
"@
    $utf8NoBom = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($OutFile, $content, $utf8NoBom)

    Write-Host "Hosting Bundle: $($aspnet.version) $($hosting.Url) ($($hosting.Size) bytes), ANCM $ancmVersion"
    Write-Host "SQL Express:    $sqlProduct $sqlVersion $($sql.Url) ($($sqlItem.Length) bytes, signature valid)"
    Write-Host "Wrote $OutFile"
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
