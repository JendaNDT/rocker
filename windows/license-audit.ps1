# SPDX-License-Identifier: AGPL-3.0-only
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ApplicationDirectory,
      [Parameter(Mandatory)][string]$AssetsFile)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$app=(Resolve-Path $ApplicationDirectory).Path
$assets=Get-Content $AssetsFile -Raw | ConvertFrom-Json -AsHashtable
$components=@()
foreach($entry in $assets.libraries.GetEnumerator() | Sort-Object Key) {
    if($entry.Value.type -ne 'package'){continue}
    $location=$null
    foreach($root in $assets.packageFolders.Keys){$candidate=Join-Path $root $entry.Value.path;if(Test-Path $candidate){$location=$candidate;break}}
    if(-not $location){throw "License audit cannot find restored package $($entry.Key)."}
    $spec=Get-ChildItem $location -File -Filter '*.nuspec' | Select-Object -First 1
    if(-not $spec){throw "Missing NuGet provenance: $($entry.Key)."}
    [xml]$xml=Get-Content $spec.FullName -Raw
    $license=@($xml.GetElementsByTagName('license'))
    $url=@($xml.GetElementsByTagName('licenseUrl'))
    if($license.Count -eq 0 -and $url.Count -eq 0){throw "Missing license declaration: $($entry.Key)."}
    $components+=@{package=$entry.Key;license=if($license.Count){$license[0].InnerText}else{$url[0].InnerText};licenseType=if($license.Count){$license[0].GetAttribute('type')}else{'url'};noticeDirectory='licenses/nuget/'+$entry.Value.path}
}
$native=@(
    @{name='Rocker native code';license='AGPL-3.0-only';source='rocker-source.zip'},
    @{name='PWA migration exporter';license='MIT';source='rocker-source.zip';notice='licenses/pwa-exporter-MIT.txt'},
    @{name='JUCE 9.0.3, audio/core/events modules';license='AGPL-3.0-only';source='JUCE-source.zip'},
    @{name='Bundled ASIO headers';license='GPL-3.0-only';source='JUCE-source.zip'},
    @{name='Ogg/Opus/Opusfile/libopusenc';license='BSD-3-Clause and patent grants';source='JUCE-source.zip'},
    @{name='zlib';license='Zlib';source='JUCE-source.zip'},
    @{name='Signalsmith Stretch/Linear';license='MIT';source='Signalsmith-Stretch-source.zip and Signalsmith-Linear-source.zip'},
    @{name='libwebm';license='BSD-3-Clause and patent grant';source='libwebm-source.zip'},
    @{name='Bravura/Noto Sans';license='SIL OFL-1.1';source='upstream font notices'},
    @{name='Inno Setup 6.7.3';license='Inno Setup License';notice='licenses/inno-setup-license.txt'}
)
$files=@(Get-ChildItem $app -File -Recurse | Where-Object {$_.Extension -in @('.dll','.exe')} | Sort-Object FullName | ForEach-Object {@{path=[IO.Path]::GetRelativePath($app,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
@{schemaVersion=1;nativeComponents=$native;restoredNuGetComponents=$components;publishedBinaries=$files;systemComponentsNotBundled=@('Windows Media Foundation codecs','M-Audio ASIO driver','Evergreen WebView2 runtime');separateCorrespondingSources=$true;modifiedUpstreamSources=$false} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $app 'component-manifest.json') -Encoding utf8
