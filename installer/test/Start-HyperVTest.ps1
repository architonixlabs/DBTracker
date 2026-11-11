#Requires -RunAsAdministrator
# Installs DBTracker-Setup.exe from scratch in a throwaway Hyper-V VM built from a Windows Server
# evaluation VHD. The base VHD is never modified: the VM boots from a differencing disk that is
# recreated on every run. Progress and results land in installer\out\hyperv-results.
param(
    [Parameter(Mandatory)][string]$VhdPath,
    [string]$VmName = 'DBTracker-Test',
    [string]$VmRoot = 'D:\DBTracker-TestVM',
    [int]$MemoryGB = 6,
    [int]$Port = 8150,
    [switch]$SkipSqlExpress
)

$ErrorActionPreference = 'Stop'
$marker = 'Created by DBTracker installer\test\Start-HyperVTest.ps1'
$installerDir = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $installerDir 'out'
$setup = Join-Path $outDir 'DBTracker-Setup.exe'
$results = Join-Path $outDir 'hyperv-results'

if (Test-Path $results) { Remove-Item $results -Recurse -Force }
New-Item -ItemType Directory -Path $results | Out-Null
Start-Transcript -Path (Join-Path $results 'host-transcript.txt') -Force | Out-Null
function Step($text) { Write-Host "[$(Get-Date -Format HH:mm:ss)] STEP: $text" }

try {
    if (-not (Test-Path $VhdPath)) { throw "VHD not found: $VhdPath" }
    if (-not (Test-Path $setup)) { throw "Build the installer first: installer\build.ps1" }

    $existing = Get-VM -Name $VmName -ErrorAction SilentlyContinue
    if ($existing) {
        if ($existing.Notes -ne $marker) { throw "A VM named '$VmName' exists that this script did not create. Pick another -VmName." }
        Step "Removing the previous $VmName VM"
        Stop-VM -Name $VmName -TurnOff -Force -ErrorAction SilentlyContinue
        $oldDisks = (Get-VMHardDiskDrive -VMName $VmName).Path
        Remove-VM -Name $VmName -Force
        $oldDisks | ForEach-Object { Remove-Item $_ -Force -ErrorAction SilentlyContinue }
    }

    New-Item -ItemType Directory -Force -Path $VmRoot | Out-Null
    $ext = [IO.Path]::GetExtension($VhdPath).ToLowerInvariant()
    $generation = if ($ext -eq '.vhdx') { 2 } else { 1 }
    $child = Join-Path $VmRoot "$VmName-run$ext"
    Step "Creating differencing disk $child (Generation $generation VM)"
    New-VHD -Path $child -ParentPath $VhdPath -Differencing | Out-Null

    $password = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 20 | ForEach-Object { [char]$_ }) + '!a1'
    Step 'Writing unattend.xml (admin password, auto logon, skip OOBE)'
    $mounted = Mount-VHD -Path $child -Passthru
    try {
        $winDrive = $mounted | Get-Disk | Get-Partition | Get-Volume |
            Where-Object { $_.DriveLetter -and (Test-Path "$($_.DriveLetter):\Windows\System32") } |
            Select-Object -First 1
        if (-not $winDrive) { throw 'Could not find the Windows partition in the VHD.' }
        $panther = "$($winDrive.DriveLetter):\Windows\Panther"
        New-Item -ItemType Directory -Force -Path $panther | Out-Null
        $unattend = @"
<?xml version="1.0" encoding="utf-8"?>
<unattend xmlns="urn:schemas-microsoft-com:unattend" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
  <settings pass="oobeSystem">
    <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <OOBE><HideEULAPage>true</HideEULAPage><SkipMachineOOBE>true</SkipMachineOOBE><SkipUserOOBE>true</SkipUserOOBE></OOBE>
      <UserAccounts><AdministratorPassword><Value>$password</Value><PlainText>true</PlainText></AdministratorPassword></UserAccounts>
      <AutoLogon><Enabled>true</Enabled><Username>Administrator</Username><Password><Value>$password</Value><PlainText>true</PlainText></Password><LogonCount>999</LogonCount></AutoLogon>
      <TimeZone>India Standard Time</TimeZone>
    </component>
    <component name="Microsoft-Windows-International-Core" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <InputLocale>en-US</InputLocale><SystemLocale>en-US</SystemLocale><UILanguage>en-US</UILanguage><UserLocale>en-US</UserLocale>
    </component>
  </settings>
</unattend>
"@
        Set-Content -Path (Join-Path $panther 'unattend.xml') -Value $unattend -Encoding UTF8
    } finally {
        Dismount-VHD -Path $child
    }

    Step "Creating and starting VM $VmName"
    $switch = (Get-VMSwitch -Name 'Default Switch' -ErrorAction SilentlyContinue).Name
    if (-not $switch) { throw "Hyper-V 'Default Switch' not found; the VM needs internet access for the downloads." }
    New-VM -Name $VmName -Generation $generation -MemoryStartupBytes ($MemoryGB * 1GB) -VHDPath $child -SwitchName $switch -Path $VmRoot | Out-Null
    Set-VM -Name $VmName -ProcessorCount 4 -Notes $marker -AutomaticCheckpointsEnabled $false
    Start-VM -Name $VmName
    Start-Process vmconnect.exe -ArgumentList 'localhost', $VmName

    $cred = New-Object PSCredential('Administrator', (ConvertTo-SecureString $password -AsPlainText -Force))
    @{ VmName = $VmName; Username = 'Administrator'; Password = $password } | ConvertTo-Json |
        Set-Content (Join-Path $results 'vm-login.json')

    Step 'Waiting for Windows to finish first boot (PowerShell Direct)'
    $deadline = (Get-Date).AddMinutes(25)
    $session = $null
    while (-not $session -and (Get-Date) -lt $deadline) {
        try { $session = New-PSSession -VMName $VmName -Credential $cred -ErrorAction Stop } catch { Start-Sleep -Seconds 15 }
    }
    if (-not $session) { throw 'The VM did not accept a PowerShell Direct session within 25 minutes.' }

    Step 'Copying the installer and test script into the VM'
    Invoke-Command -Session $session { New-Item -ItemType Directory -Force -Path C:\DBTrackerTest\Results | Out-Null }
    Copy-Item $setup -Destination C:\DBTrackerTest\ -ToSession $session
    Copy-Item (Join-Path $PSScriptRoot 'Test-InSandbox.ps1') -Destination C:\DBTrackerTest\ -ToSession $session

    Step "Running DBTracker-Setup.exe in the VM (SQL Server Express: $(-not $SkipSqlExpress))"
    $job = Invoke-Command -Session $session -AsJob -ArgumentList $Port, [bool]$SkipSqlExpress -ScriptBlock {
        param($p, $skipSql)
        Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
        $testArgs = @{ Installer = 'C:\DBTrackerTest\DBTracker-Setup.exe'; Results = 'C:\DBTrackerTest\Results'; Port = $p; NoUi = $true; SkipSqlExpress = $skipSql }
        & C:\DBTrackerTest\Test-InSandbox.ps1 @testArgs
    }
    while ($job.State -eq 'Running') {
        Start-Sleep -Seconds 30
        try { Copy-Item 'C:\DBTrackerTest\Results\DBTracker-Setup.log' -Destination $results -FromSession $session -ErrorAction Stop } catch { }
    }
    Receive-Job $job | Out-File (Join-Path $results 'vm-output.txt')

    Step 'Collecting results from the VM'
    Copy-Item 'C:\DBTrackerTest\Results\*' -Destination $results -FromSession $session -Recurse -Force
    Remove-PSSession $session
    Step "Done. The VM is still running; log in with the details in vm-login.json and open http://localhost:$Port/install inside it."
} catch {
    Write-Host "FAILED: $($_.Exception.Message)"
    @{ HostError = $_.Exception.Message } | ConvertTo-Json | Set-Content (Join-Path $results 'host-error.json')
} finally {
    Stop-Transcript | Out-Null
}
