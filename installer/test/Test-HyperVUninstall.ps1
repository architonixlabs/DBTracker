#Requires -RunAsAdministrator
# Install/uninstall test for DBTracker in the Start-HyperVTest.ps1 VM (prerequisites already present).
# 1: install with a SQL login for the app pool. 2: normal uninstall (what Settings > Apps runs).
# 3: reinstall, then `DBTracker-Setup.exe /uninstall RemoveData=1`. 4: reinstall and leave it installed,
# opening the wizard on the VM desktop. Snapshots and logs land in installer\out\hyperv-uninstall.
param([string]$VmName = 'DBTracker-Test', [int]$Port = 8150, [string]$SqlServer = '.\SQLEXPRESS')

$ErrorActionPreference = 'Stop'
$outDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'out'
$results = Join-Path $outDir 'hyperv-uninstall'
if (Test-Path $results) { Remove-Item $results -Recurse -Force }
New-Item -ItemType Directory -Path $results | Out-Null
Start-Transcript -Path (Join-Path $results 'host-transcript.txt') -Force | Out-Null
function Step($text) { Write-Host "[$(Get-Date -Format HH:mm:ss)] STEP: $text" }

# Returns a JSON string so values survive the trip back from the VM unchanged.
$snapshot = {
    param($port, $sqlServer)
    $roots = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    $arp = Get-ItemProperty $roots -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like 'DBTracker*' }
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $folder = 'C:\inetpub\DBTracker'
    $login = 'unknown'; $dbcreator = 'unknown'
    try {
        $cn = New-Object System.Data.SqlClient.SqlConnection "Server=$sqlServer;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=10"
        $cn.Open(); $cmd = $cn.CreateCommand()
        $cmd.CommandText = "SELECT (SELECT COUNT(*) FROM sys.server_principals WHERE name = N'IIS APPPOOL\DBTracker'), ISNULL(IS_SRVROLEMEMBER('dbcreator', N'IIS APPPOOL\DBTracker'), 0)"
        $r = $cmd.ExecuteReader(); [void]$r.Read()
        $login = $r.GetInt32(0) -eq 1; $dbcreator = $r.GetInt32(1) -eq 1
        $cn.Close()
    } catch { $login = "error: $($_.Exception.Message)" }
    [ordered]@{
        AppsEntries         = @($arp | ForEach-Object { "$($_.DisplayName) $($_.DisplayVersion)$(if ($_.SystemComponent -eq 1) { ' [hidden]' })" })
        Site                = [bool](Get-Website -Name 'DBTracker' -ErrorAction SilentlyContinue)
        AppPool             = [bool](Test-Path 'IIS:\AppPools\DBTracker')
        PortListening       = [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
        InstallFolder       = [bool](Test-Path $folder)
        ProgramFiles        = if (Test-Path $folder) { @(Get-ChildItem $folder -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notlike "$folder\App_Data\*" }).Count } else { 0 }
        AppData             = if (Test-Path "$folder\App_Data") { (Get-ChildItem "$folder\App_Data" -Recurse -File | ForEach-Object { $_.FullName.Substring($folder.Length + 1) }) -join '; ' } else { '(no App_Data)' }
        SettingsRegistry    = [bool](Test-Path 'HKLM:\SOFTWARE\Architonix Labs\DBTracker')
        SqlLogin            = $login
        SqlLoginDbcreator   = $dbcreator
        HostingBundle       = [bool](Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")
        SqlExpressRunning   = (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue).Status.ToString()
    } | ConvertTo-Json -Compress
}

$runSetup = {
    param($exe, $arguments, $log)
    (Start-Process $exe -ArgumentList ($arguments + @('/norestart', '/log', $log)) -Wait -PassThru).ExitCode
}

try {
    $login = Get-Content (Join-Path $outDir 'hyperv-results\vm-login.json') -Raw | ConvertFrom-Json
    $cred = New-Object PSCredential('Administrator', (ConvertTo-SecureString $login.Password -AsPlainText -Force))
    $s = New-PSSession -VMName $VmName -Credential $cred
    $setup = 'C:\DBTrackerTest\DBTracker-Setup.exe'
    $logs = 'C:\DBTrackerTest\Uninstall'
    Invoke-Command -Session $s { param($l) Remove-Item $l -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force $l | Out-Null } -ArgumentList $logs
    Copy-Item (Join-Path $outDir 'DBTracker-Setup.exe') -Destination C:\DBTrackerTest\ -ToSession $s -Force
    $installArgs = @('/quiet', 'InstallSqlExpress=0', 'LaunchWizard=0', "SitePort=$Port", "SqlLoginServer=$SqlServer")
    $report = [ordered]@{}

    Step 'Phase 0: removing any DBTracker Setup entry left from earlier runs'
    $report.Phase0_ExitCode = Invoke-Command -Session $s -ScriptBlock {
        param($l)
        $entry = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -eq 'DBTracker Setup' } | Select-Object -First 1
        if (-not $entry) { return 'nothing to remove' }
        (Start-Process $entry.BundleCachePath -ArgumentList '/uninstall', '/quiet', '/norestart', '/log', "$l\0-cleanup.log" -Wait -PassThru).ExitCode
    } -ArgumentList $logs
    $report.Phase0_After = Invoke-Command -Session $s -ScriptBlock $snapshot -ArgumentList $Port, $SqlServer

    Step "Phase 1: install with a SQL login on $SqlServer"
    $report.Phase1_ExitCode = Invoke-Command -Session $s -ScriptBlock $runSetup -ArgumentList $setup, $installArgs, "$logs\1-install.log"
    $report.Phase1_After = Invoke-Command -Session $s -ScriptBlock $snapshot -ArgumentList $Port, $SqlServer

    Step 'Phase 2: normal uninstall (the registered quiet uninstall, as Settings > Apps runs it)'
    $report.Phase2_ExitCode = Invoke-Command -Session $s -ScriptBlock {
        param($l)
        $entry = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -eq 'DBTracker Setup' } | Select-Object -First 1
        if (-not $entry) { return 'no DBTracker Setup entry' }
        (Start-Process $entry.BundleCachePath -ArgumentList '/uninstall', '/quiet', '/norestart', '/log', "$l\2-uninstall.log" -Wait -PassThru).ExitCode
    } -ArgumentList $logs
    Start-Sleep -Seconds 5
    $report.Phase2_After = Invoke-Command -Session $s -ScriptBlock $snapshot -ArgumentList $Port, $SqlServer

    Step 'Phase 3: reinstall, then uninstall with RemoveData=1'
    $report.Phase3_ReinstallExitCode = Invoke-Command -Session $s -ScriptBlock $runSetup -ArgumentList $setup, $installArgs, "$logs\3-reinstall.log"
    $report.Phase3_ExitCode = Invoke-Command -Session $s -ScriptBlock $runSetup -ArgumentList $setup, @('/uninstall', '/quiet', 'RemoveData=1'), "$logs\3-uninstall-remove-data.log"
    Start-Sleep -Seconds 5
    $report.Phase3_After = Invoke-Command -Session $s -ScriptBlock $snapshot -ArgumentList $Port, $SqlServer

    Step 'Phase 4: final install, left in place, wizard opened on the VM desktop'
    $report.Phase4_ExitCode = Invoke-Command -Session $s -ScriptBlock $runSetup -ArgumentList $setup, $installArgs, "$logs\4-install.log"
    $report.Phase4_After = Invoke-Command -Session $s -ScriptBlock $snapshot -ArgumentList $Port, $SqlServer
    Invoke-Command -Session $s -ArgumentList $Port -ScriptBlock {
        param($p)
        $key = 'C:\inetpub\DBTracker\App_Data\setup-key.txt'
        for ($i = 0; $i -lt 20 -and -not (Test-Path $key); $i++) {
            try { Invoke-WebRequest "http://localhost:$p/install" -UseBasicParsing -TimeoutSec 30 | Out-Null } catch { }
            Start-Sleep -Seconds 3
        }
        $cmd = "Start-Process notepad.exe '$key'; Start-Process 'http://localhost:$p/install'"
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -WindowStyle Hidden -Command `"$cmd`""
        $principal = New-ScheduledTaskPrincipal -UserId 'Administrator' -LogonType Interactive
        Register-ScheduledTask -TaskName 'DBTrackerOpenWizard' -Action $action -Principal $principal -Force | Out-Null
        Start-ScheduledTask -TaskName 'DBTrackerOpenWizard'
        Start-Sleep -Seconds 5
        Unregister-ScheduledTask -TaskName 'DBTrackerOpenWizard' -Confirm:$false
    }

    Copy-Item "$logs\*" -Destination $results -FromSession $s -Recurse -Force
    Remove-PSSession $s
    $json = '{' + (($report.GetEnumerator() | ForEach-Object {
        $value = if ($_.Value -is [string] -and $_.Value.StartsWith('{')) { $_.Value } else { ConvertTo-Json $_.Value -Compress }
        "`"$($_.Key)`":$value"
    }) -join ',') + '}'
    Set-Content (Join-Path $results 'results.json') -Value $json -Encoding UTF8
    Step 'Done'
} catch {
    Write-Host "FAILED: $($_.Exception.Message)"
    @{ HostError = $_.Exception.Message } | ConvertTo-Json | Set-Content (Join-Path $results 'host-error.json')
} finally {
    Stop-Transcript | Out-Null
}
