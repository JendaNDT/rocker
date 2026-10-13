# SPDX-License-Identifier: AGPL-3.0-only
[CmdletBinding()]
param([string]$Reports=(Join-Path $PSScriptRoot 'out/reports'),[string]$Packages=(Join-Path $PSScriptRoot 'out/packages'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$checks=@('application-services.json','application-host.json','desktop-runtime.json','desktop-functional-coverage.json','domain-checks.json','storage-checks.json','notation-checks.json','media-checks.json','transport-checks.json','browser-ui-import.json','browser-bookmark-import.json','power-recovery.json','performance-checks.json','accessibility.json','design-views.json','ux-layout.json','workspace-enhancements.json','retro-instruments.json','metronome-visualization.json','visual-finish.json','desktop-host-experiment.json','practice-profile.json','overview-live.json','first-run.json','file-commands.json','window-smoke.json','installer/installer-smoke.json')
$results=@()
foreach($name in $checks){$value=Get-Content (Join-Path $Reports $name) -Raw | ConvertFrom-Json;if($value.result -ne 'passed'){throw "Candidate check failed: $name"};$results+=@{report=$name;result=$value.result}}
$coverage=Get-Content (Join-Path $Reports 'desktop-functional-coverage.json') -Raw | ConvertFrom-Json
$expected=@(1..32 | ForEach-Object { $_.ToString('00') })
$actual=@($coverage.views | ForEach-Object {$_.viewId} | Sort-Object -Unique)
if($coverage.viewCount -ne $expected.Count -or (Compare-Object $expected $actual) -or @($coverage.views | Where-Object {-not $_.rendered -or $_.widthDip -le 0 -or $_.heightDip -le 0}).Count -gt 0){throw 'Every desktop function must have actual Windows coverage.'}
foreach($view in $coverage.views){if(-not (Test-Path (Join-Path $Reports $view.file))){throw "Missing desktop capture: $($view.viewId)"}}
[xml]$native=Get-Content (Join-Path $Reports 'native-tests.xml') -Raw
if([int]$native.testsuite.failures -ne 0 -or [int]$native.testsuite.tests -ne 22){throw 'All 22 Windows native suites must pass.'}
$installer=Get-Content (Join-Path $Reports 'installer/installer-smoke.json') -Raw | ConvertFrom-Json
if(-not $installer.versionTransitionPreservedDataAndPriorBinary -or -not $installer.failedUpdatePreservedShortcutAndAssociation -or -not $installer.installedInventoryMatchesPortableArchive){throw 'Version transition and failed-update safety must be checked.'}
$setup=Get-ChildItem $Packages -Filter 'Rocker-Windows-Setup-*-x64.exe' | Select-Object -First 1
if(-not $setup){throw 'A compiled installer is required before candidate handoff.'}
foreach($name in @('Rocker-Windows-x64.zip','Rocker-Windows-sources.zip','SHA256SUMS.txt')){if(-not (Test-Path (Join-Path $Packages $name))){throw "Candidate package is missing: $name"}}
$source=[IO.Compression.ZipFile]::OpenRead((Join-Path $Packages 'Rocker-Windows-sources.zip'));try{if(-not $source.GetEntry('rocker-source.zip')){throw 'Corresponding sources are missing.'}}finally{$source.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $Packages 'Rocker-Windows-x64.zip'))
try{
    foreach($name in @('component-manifest.json','licenses/pwa-exporter-MIT.txt','licenses/inno-setup-license.txt','PwaMigration/helper.html','help/user-guide.txt','help/brother-test.txt','build-info.json')){if(-not $archive.GetEntry($name)){throw "Required runtime/license file is missing: $name"}}
    $reader=[IO.StreamReader]::new($archive.GetEntry('build-info.json').Open());try{$info=$reader.ReadToEnd() | ConvertFrom-Json}finally{$reader.Dispose()}
}finally{$archive.Dispose()}
@{result='passed';sourceCommit=$info.commit;windowsRun=$info.runUrl;nativeWorkspaces=32;nativeSuites=[int]$native.testsuite.tests;reports=$results;installer=$setup.Name;correspondingSourcesSeparate=$true;manualHardwareVerified=$false;actualLatencyVerified=$false;onlineAiVerified=$false;onlineYouTubeVerified=$false;narratorAndHighContrastManualVerified=$false;wallClockHourOnHardwareVerified=$false;smartAppControlVerified=$false} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $Reports 'candidate-validation.json')
