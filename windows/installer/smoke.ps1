# SPDX-License-Identifier: AGPL-3.0-only
[CmdletBinding()]
param([string]$PackageDirectory=(Join-Path $PSScriptRoot '../out/packages'),
      [string]$ReportDirectory=(Join-Path $PSScriptRoot '../out/reports/installer'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
New-Item -ItemType Directory -Force $ReportDirectory | Out-Null
$setup=Get-ChildItem $PackageDirectory -Filter 'Rocker-Windows-Setup-*-x64.exe' | Select-Object -First 1
if(-not $setup){throw 'Compiled installer missing.'}
$install=Join-Path $env:LOCALAPPDATA ('Programs/Rocker-CI-'+[Guid]::NewGuid().ToString('N'))
$arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$install`"")
function Run-Setup {
    $result=Start-Process $setup.FullName -ArgumentList $arguments -Wait -PassThru
    if($result.ExitCode -ne 0){throw "Installer failed: $($result.ExitCode)."}
}
$oldData=$env:ROCKER_USER_DATA_DIR;$oldActivation=$env:ROCKER_ACTIVATION_REPORT
$profile=Join-Path $ReportDirectory 'installed/isolated-shell-data'
$process=$null
try {
    Run-Setup
    $version=Get-ChildItem (Join-Path $install 'versions') -Directory | Select-Object -First 1
    $executable=Join-Path $version.FullName 'Rocker.Probe.exe'
    if(-not (Test-Path $executable)){throw 'Installer omitted the compiled application.'}
    & (Join-Path $PSScriptRoot '../smoke.ps1') -PublishDirectory $version.FullName -ReportDirectory (Join-Path $ReportDirectory 'installed')
    $sentinel=Join-Path $profile 'keep-after-uninstall.txt';'Preserve owned data' | Set-Content $sentinel
    $sentinelHash=(Get-FileHash $sentinel -Algorithm SHA256).Hash
    $env:ROCKER_USER_DATA_DIR=$profile
    $beforeBackups=@(Get-ChildItem (Join-Path $profile 'backups') -Filter update-backup.json -Recurse -ErrorAction SilentlyContinue).Count
    $audio=Join-Path $version.FullName 'rocker_audio.dll';$audioHash=(Get-FileHash $audio -Algorithm SHA256).Hash
    [IO.File]::WriteAllBytes($audio,[byte[]]@(1,2,3))
    Run-Setup
    if((Get-FileHash $audio -Algorithm SHA256).Hash -ne $audioHash){throw 'Repair failed to restore the exact native library.'}
    $newBackups=@(Get-ChildItem (Join-Path $profile 'backups') -Filter update-backup.json -Recurse)
    if($newBackups.Count -le $beforeBackups){throw 'Repair/update did not create the full cold backup.'}
    $latest=Get-Content ($newBackups | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).FullName -Raw | ConvertFrom-Json
    if((Get-FileHash $latest.Archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $latest.ArchiveSha256 -or -not (Test-Path $latest.DatabaseCopy)){throw 'Update backup integrity/SQLite copy is missing.'}
    $association=Get-ItemPropertyValue -Path 'HKCU:/Software/Classes/JendaNDT.Rocker.Project/shell/open/command' -Name '(default)'
    if(-not $association.Contains($executable) -or -not $association.Contains('"%1"')){throw 'Project association command is missing a quoted path.'}
    $project=Join-Path $ReportDirectory 'žluťoučký projekt.rockerproj';$now=[DateTimeOffset]::UtcNow.ToString('o')
    @{schemaVersion=1;id=[Guid]::NewGuid().ToString();name='Asociace projektu';createdAt=$now;updatedAt=$now;sampleRate=48000;tracks=@()} | ConvertTo-Json -Depth 5 | Set-Content $project -Encoding utf8
    $env:ROCKER_USER_DATA_DIR=$profile;$env:ROCKER_ACTIVATION_REPORT=Join-Path $ReportDirectory 'project-activation.json'
    $process=Start-Process $executable -ArgumentList "`"$project`"" -PassThru
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    while(-not (Test-Path $env:ROCKER_ACTIVATION_REPORT) -and [DateTime]::UtcNow -lt $deadline){if($process.HasExited){throw 'Installed file activation exited unexpectedly.'};Start-Sleep -Milliseconds 100}
    $activation=Get-Content $env:ROCKER_ACTIVATION_REPORT -Raw | ConvertFrom-Json
    if($activation.result -ne 'passed' -or $activation.kind -ne 'Project' -or $activation.view -ne '15'){throw 'Installed project file did not open the native Studio.'}
    $process.Refresh();if(-not $process.CloseMainWindow()){throw 'Installed application has no closable native window.'};if(-not $process.WaitForExit(30000) -or $process.ExitCode -ne 0){throw 'Installed application failed graceful close.'}
    $uninstall=Start-Process (Join-Path $install 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
    if($uninstall.ExitCode -ne 0 -or (Test-Path $executable)){throw 'Uninstall left the installed executable.'}
    if((Get-FileHash $sentinel -Algorithm SHA256).Hash -ne $sentinelHash -or -not (Test-Path (Join-Path $profile 'database/rocker.db'))){throw 'Uninstall removed user-owned data.'}
    @{result='passed';perUserInstall=$true;installedNativeWindow=$true;offlineSelfContainedStart=$true;exactDllRepair=$true;preUpdateFullBackupAndRawDatabase=$true;projectAssociationRegistered=$true;unicodeProjectActivation=$true;uninstallPreservedDatabaseAndSentinel=$true;adminRequired=$false;smartAppControlVerified=$false;hardwareVerified=$false} | ConvertTo-Json | Set-Content (Join-Path $ReportDirectory 'installer-smoke.json')
} finally {
    $env:ROCKER_USER_DATA_DIR=$oldData;$env:ROCKER_ACTIVATION_REPORT=$oldActivation
    if($process -and -not $process.HasExited){Stop-Process -Id $process.Id -Force}
}
