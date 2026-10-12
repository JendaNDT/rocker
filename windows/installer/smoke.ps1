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
$script:setupRun=0
function Run-Setup([string]$File=$setup.FullName) {
    ++$script:setupRun
    $log=Join-Path $ReportDirectory ("setup-$($script:setupRun).log")
    $result=Start-Process $File -ArgumentList ($arguments+@("/LOG=`"$log`"")) -Wait -PassThru
    if($result.ExitCode -ne 0){if(Test-Path $log){Get-Content $log | Select-Object -Last 30};throw "Installer failed: $($result.ExitCode)."}
}
$oldData=$env:ROCKER_USER_DATA_DIR;$oldActivation=$env:ROCKER_ACTIVATION_REPORT
$profile=Join-Path $ReportDirectory 'installed/isolated-shell-data'
$process=$null
try {
    # Exercise the version transition with explicitly synthetic installer labels;
    # both fixtures contain this candidate's exact binaries, not historic builds.
    $fixture=Join-Path $PSScriptRoot '../out/installer-transition-fixtures'
    $app=Join-Path $fixture 'application'
    Expand-Archive (Join-Path $PackageDirectory 'Rocker-Windows-x64.zip') $app
    $info=Get-Content (Join-Path $app 'build-info.json') -Raw | ConvertFrom-Json
    $previousPackages=Join-Path $fixture 'previous';New-Item -ItemType Directory -Force $previousPackages | Out-Null
    & (Join-Path $PSScriptRoot 'build.ps1') -ApplicationDirectory $app -PackageDirectory $previousPackages -Version '0.1.99' -Revision $info.commit
    $transitionProfile=Join-Path $ReportDirectory 'transition-profile'
    Copy-Item (Join-Path $PSScriptRoot '../out/reports/isolated-shell-data') $transitionProfile -Recurse
    $env:ROCKER_USER_DATA_DIR=$transitionProfile
    $transitionSentinel=Join-Path $transitionProfile 'upgrade-sentinel.txt';'Upgrade must retain this data' | Set-Content $transitionSentinel
    $transitionHash=(Get-FileHash $transitionSentinel -Algorithm SHA256).Hash
    Run-Setup (Join-Path $previousPackages 'Rocker-Windows-Setup-0.1.99-x64.exe')
    $previousPath=Get-ItemPropertyValue 'HKCU:/Software/JendaNDT/Rocker' -Name CurrentPath
    $beforeUpgrade=@(Get-ChildItem (Join-Path $transitionProfile 'backups') -Filter update-backup.json -Recurse).Count
    Run-Setup
    $currentPath=Get-ItemPropertyValue 'HKCU:/Software/JendaNDT/Rocker' -Name CurrentPath
    if($currentPath -eq $previousPath -or (Get-ItemPropertyValue 'HKCU:/Software/JendaNDT/Rocker' -Name PreviousPath) -ne $previousPath -or -not (Test-Path (Join-Path $previousPath 'Rocker.Probe.exe'))){throw 'Upgrade lost the previous executable/path.'}
    if((Get-FileHash $transitionSentinel -Algorithm SHA256).Hash -ne $transitionHash -or @(Get-ChildItem (Join-Path $transitionProfile 'backups') -Filter update-backup.json -Recurse).Count -le $beforeUpgrade){throw 'Version transition did not preserve data and create a new backup.'}
    $version=Get-Item $currentPath
    $executable=Join-Path $version.FullName 'Rocker.Probe.exe'
    $currentAssociation=Get-ItemPropertyValue 'HKCU:/Software/Classes/JendaNDT.Rocker.Project/shell/open/command' -Name '(default)'
    $shortcut=Join-Path $env:APPDATA 'Microsoft/Windows/Start Menu/Programs/Rocker/Rocker.lnk'
    $shortcutHash=(Get-FileHash $shortcut -Algorithm SHA256).Hash
    $futurePackages=Join-Path $fixture 'future';New-Item -ItemType Directory -Force $futurePackages | Out-Null
    & (Join-Path $PSScriptRoot 'build.ps1') -ApplicationDirectory $app -PackageDirectory $futurePackages -Version '0.2.1' -Revision $info.commit
    $invalidProfile=Join-Path $fixture 'incompatible-profile';New-Item -ItemType Directory -Force (Join-Path $invalidProfile 'database') | Out-Null
    $invalidDatabase=Join-Path $invalidProfile 'database/rocker.db';[IO.File]::WriteAllBytes($invalidDatabase,[byte[]]@(1,2,3));$invalidHash=(Get-FileHash $invalidDatabase -Algorithm SHA256).Hash
    $env:ROCKER_USER_DATA_DIR=$invalidProfile
    $failed=Start-Process (Join-Path $futurePackages 'Rocker-Windows-Setup-0.2.1-x64.exe') -ArgumentList ($arguments+@("/LOG=`"$(Join-Path $ReportDirectory 'failed-update.log')`"")) -Wait -PassThru
    if($failed.ExitCode -eq 0 -or (Get-FileHash $invalidDatabase -Algorithm SHA256).Hash -ne $invalidHash){throw 'An invalid profile was accepted or changed by update preflight.'}
    if(Test-Path (Join-Path $install ('versions/0.2.1-'+$info.commit.Substring(0,12)))){throw 'Failed preparation published candidate files into the installation.'}
    if((Get-ItemPropertyValue 'HKCU:/Software/JendaNDT/Rocker' -Name CurrentPath) -ne $currentPath -or (Get-ItemPropertyValue 'HKCU:/Software/Classes/JendaNDT.Rocker.Project/shell/open/command' -Name '(default)') -ne $currentAssociation -or (Get-FileHash $shortcut -Algorithm SHA256).Hash -ne $shortcutHash){throw 'Failed update changed the current path, shortcut or project association.'}
    $env:ROCKER_USER_DATA_DIR=$oldData
    # Continue with the actual candidate, installed forms, repair and file activation.
    $version=Get-Item $currentPath
    $executable=Join-Path $version.FullName 'Rocker.Probe.exe'
    if(-not (Test-Path $executable)){throw 'Installer omitted the compiled application.'}
    $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $PackageDirectory 'Rocker-Windows-x64.zip'))
    try{
        foreach($entry in $archive.Entries){
            if(-not $entry.Name){continue}
            $target=Join-Path $version.FullName $entry.FullName
            if(-not (Test-Path $target)){throw "Installer omitted runtime/notice file: $($entry.FullName)"}
            $stream=$entry.Open();try{$expected=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
            if((Get-FileHash $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw "Installed inventory mismatch: $($entry.FullName)"}
        }
    }finally{$archive.Dispose()}
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
    @{result='passed';perUserInstall=$true;installedNativeWindow=$true;offlineSelfContainedStart=$true;installedInventoryMatchesPortableArchive=$true;exactDllRepair=$true;versionTransitionPreservedDataAndPriorBinary=$true;failedUpdatePreservedShortcutAndAssociation=$true;transitionUsesSameCandidateBinariesWithSyntheticVersionLabels=$true;historicBinaryDowngradeVerified=$false;preUpdateFullBackupAndRawDatabase=$true;projectAssociationRegistered=$true;unicodeProjectActivation=$true;uninstallPreservedDatabaseAndSentinel=$true;adminRequired=$false;smartAppControlVerified=$false;hardwareVerified=$false} | ConvertTo-Json | Set-Content (Join-Path $ReportDirectory 'installer-smoke.json')
} finally {
    $env:ROCKER_USER_DATA_DIR=$oldData;$env:ROCKER_ACTIVATION_REPORT=$oldActivation
    if($process -and -not $process.HasExited){Stop-Process -Id $process.Id -Force}
}
