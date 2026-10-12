# SPDX-License-Identifier: AGPL-3.0-only
[CmdletBinding()]
param([string]$Reports=(Join-Path $PSScriptRoot 'out/reports'),[string]$Packages=(Join-Path $PSScriptRoot 'out/packages'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$checks=@('domain-checks.json','storage-checks.json','notation-checks.json','media-checks.json','transport-checks.json','browser-ui-import.json','browser-bookmark-import.json','power-recovery.json','performance-checks.json','accessibility.json','design-views.json','practice-profile.json','overview-live.json','first-run.json','file-commands.json','window-smoke.json','installer/installer-smoke.json')
$results=@()
foreach($name in $checks){$value=Get-Content (Join-Path $Reports $name) -Raw | ConvertFrom-Json;if($value.result -ne 'passed'){throw "Candidate check failed: $name"};$results+=@{report=$name;result=$value.result}}
$design=Get-Content (Join-Path $Reports 'design-views.json') -Raw | ConvertFrom-Json
if($design.nativeViews -ne 32 -or $design.screenshots.Count -ne 35){throw 'Candidate does not cover every native workspace.'}
[xml]$native=Get-Content (Join-Path $Reports 'native-tests.xml') -Raw
if([int]$native.testsuite.failures -ne 0){throw 'Native checks have failures.'}
$setup=Get-ChildItem $Packages -Filter 'Rocker-Windows-Setup-*-x64.exe' | Select-Object -First 1
if(-not $setup){throw 'A compiled installer is required before candidate handoff.'}
foreach($name in @('Rocker-Windows-x64.zip','Rocker-Windows-sources.zip','SHA256SUMS.txt')){if(-not (Test-Path (Join-Path $Packages $name))){throw "Candidate package is missing: $name"}}
$source=[IO.Compression.ZipFile]::OpenRead((Join-Path $Packages 'Rocker-Windows-sources.zip'));try{if(-not $source.GetEntry('rocker-source.zip')){throw 'Corresponding sources are missing.'}}finally{$source.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $Packages 'Rocker-Windows-x64.zip'))
try{
    foreach($name in @('component-manifest.json','licenses/pwa-exporter-MIT.txt','licenses/inno-setup-license.txt','PwaMigration/helper.html','build-info.json')){if(-not $archive.GetEntry($name)){throw "Required runtime/license file is missing: $name"}}
    $reader=[IO.StreamReader]::new($archive.GetEntry('build-info.json').Open());try{$info=$reader.ReadToEnd() | ConvertFrom-Json}finally{$reader.Dispose()}
}finally{$archive.Dispose()}
@{result='passed';sourceCommit=$info.commit;windowsRun=$info.runUrl;nativeWorkspaces=32;nativeSuites=[int]$native.testsuite.tests;reports=$results;installer=$setup.Name;correspondingSourcesSeparate=$true;manualHardwareVerified=$false;actualLatencyVerified=$false;onlineAiVerified=$false;onlineYouTubeVerified=$false;narratorAndHighContrastManualVerified=$false;wallClockHourOnHardwareVerified=$false;smartAppControlVerified=$false} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $Reports 'candidate-validation.json')
