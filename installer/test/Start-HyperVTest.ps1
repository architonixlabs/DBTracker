#Requires -RunAsAdministrator
# Installs DBTracker-Setup.exe from scratch in a throwaway Hyper-V VM built from a Windows Server
# evaluation VHD. The base VHD is never modified: the VM boots from a differencing disk. Setup runs on
# the VM's desktop with its progress window visible (watch it in the console), then Edge opens the
# setup wizard. Progress and results land in installer\out\hyperv-results.
param(
    [Parameter(Mandatory)][string]$VhdPath,
    [string]$VmName = 'DBTracker-Test',
    [string]$VmRoot = 'D:\DBTracker-TestVM',
    [int]$MemoryGB = 6,
    [int]$Port = 8150,
    [switch]$SkipSqlExpress,
    # Keep the existing test VM (skips the ~10 minute first boot) instead of rebuilding it.
    [switch]$ReuseVm
)

$ErrorActionPreference = 'Stop'
$marker = 'Created by DBTracker installer\test\Start-HyperVTest.ps1'
$installerDir = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $installerDir 'out'
$setup = Join-Path $outDir 'DBTracker-Setup.exe'
$results = Join-Path $outDir 'hyperv-results'
$loginFile = Join-Path $results 'vm-login.json'

$password = $null
$existing = Get-VM -Name $VmName -ErrorAction SilentlyContinue
if ($ReuseVm -and $existing -and $existing.Notes -eq $marker -and (Test-Path $loginFile)) {
    $password = (Get-Content $loginFile -Raw | ConvertFrom-Json).Password
}

if (Test-Path $results) { Remove-Item $results -Recurse -Force }
New-Item -ItemType Directory -Path $results | Out-Null
Start-Transcript -Path (Join-Path $results 'host-transcript.txt') -Force | Out-Null
function Step($text) { Write-Host "[$(Get-Date -Format HH:mm:ss)] STEP: $text" }

function Wait-Session($cred, [int]$minutes) {
    $deadline = (Get-Date).AddMinutes($minutes)
    while ((Get-Date) -lt $deadline) {
        try { return New-PSSession -VMName $VmName -Credential $cred -ErrorAction Stop } catch { Start-Sleep -Seconds 15 }
    }
    throw "The VM did not accept a PowerShell Direct session within $minutes minutes."
}

try {
    if (-not (Test-Path $setup)) { throw 'Build the installer first: installer\build.ps1' }

    if ($password) {
        Step "Reusing the existing $VmName VM"
        if ((Get-VM -Name $VmName).State -ne 'Running') { Start-VM -Name $VmName }
    } else {
        if (-not (Test-Path $VhdPath)) { throw "VHD not found: $VhdPath" }
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
        Step 'Writing unattend.xml (admin password, skip OOBE)'
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
    }

    @{ VmName = $VmName; Username = 'Administrator'; Password = $password } | ConvertTo-Json | Set-Content $loginFile
    if (-not (Get-Process vmconnect -ErrorAction SilentlyContinue)) { Start-Process vmconnect.exe -ArgumentList 'localhost', $VmName }
    $cred = New-Object PSCredential('Administrator', (ConvertTo-SecureString $password -AsPlainText -Force))

    Step 'Waiting for Windows (PowerShell Direct)'
    $session = Wait-Session $cred 25

    Step 'Turning on automatic sign-in for Administrator and restarting the VM'
    Invoke-Command -Session $session -ArgumentList $password -ScriptBlock {
        param($pw)
        $wl = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
        Set-ItemProperty $wl -Name AutoAdminLogon -Value '1'
        Set-ItemProperty $wl -Name DefaultUserName -Value 'Administrator'
        Set-ItemProperty $wl -Name DefaultPassword -Value $pw
        Set-ItemProperty $wl -Name DefaultDomainName -Value $env:COMPUTERNAME
        # Server Manager pops up at every sign-in and would cover the installer window.
        New-ItemProperty 'HKLM:\SOFTWARE\Microsoft\ServerManager' -Name DoNotOpenServerManagerAtLogon -Value 1 -PropertyType DWord -Force | Out-Null
    }
    Remove-PSSession $session
    Restart-VM -Name $VmName -Force
    Start-Sleep -Seconds 20
    $session = Wait-Session $cred 15

    Step 'Waiting for the automatic sign-in to reach the desktop'
    $deadline = (Get-Date).AddMinutes(5)
    while (-not (Invoke-Command -Session $session { [bool](Get-Process explorer -ErrorAction SilentlyContinue) }) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 10
    }
    if (-not (Invoke-Command -Session $session { [bool](Get-Process explorer -ErrorAction SilentlyContinue) })) {
        throw 'Automatic sign-in did not reach the desktop within 5 minutes.'
    }

    Step 'Copying the installer and test script into the VM'
    Invoke-Command -Session $session {
        Remove-Item C:\DBTrackerTest -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path C:\DBTrackerTest\Results | Out-Null
    }
    Copy-Item $setup -Destination C:\DBTrackerTest\ -ToSession $session
    Copy-Item (Join-Path $PSScriptRoot 'Test-InSandbox.ps1') -Destination C:\DBTrackerTest\ -ToSession $session

    Step "Starting DBTracker-Setup.exe on the VM desktop (SQL Server Express: $(-not $SkipSqlExpress))"
    # A scheduled task with an interactive logon puts Setup's progress window on the signed-in desktop,
    # which PowerShell Direct alone cannot do.
    Invoke-Command -Session $session -ArgumentList $Port, $SkipSqlExpress.IsPresent -ScriptBlock {
        param($p, $skipSql)
        $arguments = "-NoProfile -ExecutionPolicy Bypass -File C:\DBTrackerTest\Test-InSandbox.ps1 -Installer C:\DBTrackerTest\DBTracker-Setup.exe -Results C:\DBTrackerTest\Results -Port $p -Passive"
        if ($skipSql) { $arguments += ' -SkipSqlExpress' }
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
        $principal = New-ScheduledTaskPrincipal -UserId 'Administrator' -LogonType Interactive -RunLevel Highest
        Register-ScheduledTask -TaskName 'DBTrackerInstallTest' -Action $action -Principal $principal -Force | Out-Null
        Start-ScheduledTask -TaskName 'DBTrackerInstallTest'
    }

    Step 'Installing (watch the VM console); streaming the Setup log back every 30 seconds'
    $deadline = (Get-Date).AddMinutes(90)
    $done = $false
    while (-not $done -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 30
        try { Copy-Item 'C:\DBTrackerTest\Results\DBTracker-Setup.log' -Destination $results -FromSession $session -ErrorAction Stop } catch { }
        $done = Invoke-Command -Session $session { Test-Path C:\DBTrackerTest\Results\results.json }
    }
    if (-not $done) { throw 'Setup did not finish within 90 minutes.' }

    Step 'Collecting results from the VM'
    Copy-Item 'C:\DBTrackerTest\Results\*' -Destination $results -FromSession $session -Recurse -Force
    Invoke-Command -Session $session { Unregister-ScheduledTask -TaskName 'DBTrackerInstallTest' -Confirm:$false -ErrorAction SilentlyContinue }
    Remove-PSSession $session
    Step "Done. The wizard should be open in Edge inside the VM (http://localhost:$Port/install)."
} catch {
    Write-Host "FAILED: $($_.Exception.Message)"
    @{ HostError = $_.Exception.Message } | ConvertTo-Json | Set-Content (Join-Path $results 'host-error.json')
} finally {
    Stop-Transcript | Out-Null
}
