# SPDX-License-Identifier: AGPL-3.0-only
# Package only after build.ps1 and smoke.ps1 have succeeded on Windows.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $PSScriptRoot 'out/probe'
$reports = Join-Path $PSScriptRoot 'out/reports'
$packages = Join-Path $PSScriptRoot 'out/packages'
$stage = Join-Path $PSScriptRoot ('out/package-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'application'
$sources = Join-Path $stage 'sources'
$juce = Join-Path $PSScriptRoot 'out/native/_deps/juce-src'
function Invoke-Git([string[]]$Arguments) {
    $result = & git @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Git archive or revision lookup failed.' }
    return $result
}
$commit = Invoke-Git @('-C', $repository, 'rev-parse', 'HEAD')
$juceCommit = Invoke-Git @('-C', $juce, 'rev-parse', 'HEAD')
$mediaSources = @(
    @{ Name = 'Signalsmith-Stretch'; Directory = 'signalsmith-stretch-src'; Commit = 'a670068d9aeb64913331d5cc29337b19a457a7df' },
    @{ Name = 'Signalsmith-Linear'; Directory = 'signalsmith-linear-src'; Commit = 'de55e6a50ffcf6f8f43f649692d94691c7025151' },
    @{ Name = 'libwebm'; Directory = 'libwebm-src'; Commit = '6745fd29e0245fc584b0bb9f65018ea2366fe7fb' }
)
$notationSources = @(
    @{ Name = 'AlphaTab'; Repository = 'https://github.com/CoderLine/alphaTab.git'; Tag = 'v1.8.3'; Commit = '9d0142eee2b1175dfd36e839ddbd867aed9e454e' },
    @{ Name = 'AlphaSkia'; Repository = 'https://github.com/CoderLine/alphaSkia.git'; Tag = 'v3.4.135'; Commit = 'c82e319faeba4155b45664766e9badf528c0fc49' }
)
$expectedJuce = [regex]::Match((Get-Content (Join-Path $PSScriptRoot 'CMakeLists.txt') -Raw), 'GIT_TAG ([a-f0-9]{40})').Groups[1].Value
if ($juceCommit -ne $expectedJuce) { throw 'JUCE sources do not match the configured revision.' }
$smoke = Get-Content (Join-Path $reports 'window-smoke.json') -Raw | ConvertFrom-Json
if ($smoke.result -ne 'passed') { throw 'Window smoke test has not passed.' }
foreach ($required in @('Rocker.Probe.exe', 'rocker_audio.dll', 'Rocker.Probe.runtimeconfig.json', 'libalphaskia.dll')) {
    if (-not (Test-Path (Join-Path $publish $required))) { throw "Missing application file: $required" }
}
if (Get-ChildItem $publish -Filter 'rocker_audio_test*' -Recurse) { throw 'Test backend found in publish output.' }
New-Item -ItemType Directory -Force -Path $app, $sources, $packages | Out-Null
Copy-Item (Join-Path $publish '*') $app -Recurse
$licenses = Join-Path $app 'licenses'
New-Item -ItemType Directory -Force -Path $licenses | Out-Null
foreach ($codec in @('ogg', 'opus/opus', 'opus/opusfile', 'opus/libopusenc')) {
    $name = $codec.Replace('/', '-')
    Copy-Item (Join-Path $juce "modules/juce_audio_formats/codecs/$codec/COPYING") (Join-Path $licenses "$name-license.txt")
}
Copy-Item (Join-Path $juce 'modules/juce_audio_formats/codecs/opus/opus/LICENSE_PLEASE_READ.txt') (Join-Path $licenses 'opus-patent-information.txt')
foreach ($dependency in $mediaSources) {
    $checkout = Join-Path $PSScriptRoot ('out/native/_deps/' + $dependency.Directory)
    $revision = Invoke-Git @('-C', $checkout, 'rev-parse', 'HEAD')
    if ($revision -ne $dependency.Commit) { throw "$($dependency.Name) revision mismatch." }
    if ($dependency.Name -eq 'libwebm') {
        Copy-Item (Join-Path $checkout 'LICENSE.TXT') (Join-Path $licenses 'libwebm-license.txt')
        Copy-Item (Join-Path $checkout 'PATENTS.TXT') (Join-Path $licenses 'libwebm-patents.txt')
    } else {
        Copy-Item (Join-Path $checkout 'LICENSE.txt') (Join-Path $licenses ($dependency.Name + '-license.txt'))
    }
    Invoke-Git @('-C', $checkout, 'archive', '--format=zip', "--output=$(Join-Path $sources ($dependency.Name + '-source.zip'))", 'HEAD')
}
$evidence = Join-Path $repository 'docs/windows/decisions/evidence'
foreach ($name in @('AGPL-3.0-only.txt', 'GPL-3.0-only.txt', 'juce-license.txt', 'juce.spdx.json',
    'asio-license.txt', 'windowsapp-license.txt', 'webview-sdk-license.txt')) {
    Copy-Item (Join-Path $evidence $name) $licenses
}
Copy-Item (Join-Path $repository 'docs/windows/w09/evidence/Apache-2.0.txt') (Join-Path $licenses 'SQLitePCLRaw-Apache-2.0.txt')
$notationEvidence = Join-Path $repository 'docs/windows/w04/evidence'
foreach ($name in @('alphatab-license.txt', 'alphaskia-license.txt', 'bravura-license.txt')) {
    Copy-Item (Join-Path $notationEvidence $name) $licenses
}
Copy-Item (Join-Path $PSScriptRoot 'Rocker.Notation/fonts/OFL.txt') (Join-Path $licenses 'noto-sans-license.txt')
@"
AlphaTab 1.8.3: MPL-2.0, unmodified NuGet package.
Corresponding source: AlphaTab-source.zip in the separate sources package.
https://github.com/CoderLine/alphaTab/tree/9d0142eee2b1175dfd36e839ddbd867aed9e454e
AlphaSkia 3.4.135: BSD-3-Clause with Skia and HarfBuzz notices in alphaskia-license.txt.
https://github.com/CoderLine/alphaSkia/tree/c82e319faeba4155b45664766e9badf528c0fc49
Embedded Bravura and Noto Sans fonts: SIL Open Font License, see included notices.
"@ | Set-Content (Join-Path $licenses 'notation-sources.txt')

# Preserve package license notices from the packages actually restored for this app.
$assets = Get-Content (Join-Path $PSScriptRoot 'Rocker.Probe/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$packagePaths = @($assets.libraries.Values | Where-Object { $_.type -eq 'package' } | ForEach-Object { $_.path })
$runtime = Get-Content (Join-Path $publish 'Rocker.Probe.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $packagePaths += "$($framework.name.ToLowerInvariant()).runtime.win-x64/$($framework.version)"
}
foreach ($root in $assets.packageFolders.Keys) {
    foreach ($relative in ($packagePaths | Sort-Object -Unique)) {
        $directory = Join-Path $root $relative
        if (-not (Test-Path $directory)) { continue }
        Get-ChildItem $directory -Recurse -File | Where-Object {
            $_.Name -match '^(LICENSE|LICENCE|NOTICE|COPYRIGHT|ThirdPartyNotices|THIRD-PARTY-NOTICES)(\.|$|-)' -or $_.Extension -eq '.nuspec'
        } | ForEach-Object {
            $destination = Join-Path $licenses (Join-Path 'nuget' (Join-Path $relative ([IO.Path]::GetRelativePath($directory, $_.FullName))))
            New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
            Copy-Item $_.FullName $destination
        }
    }
}
# zlib's upstream notice is embedded in its header, rather than a separate license file.
Get-ChildItem (Join-Path $juce 'modules/juce_core/zip') -Recurse -File -Filter '*zlib*.h' | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $licenses ($_.Name + '.txt'))
}
Copy-Item (Join-Path $repository 'LICENSE') (Join-Path $licenses 'pwa-exporter-MIT.txt')
Copy-Item (Join-Path $repository 'docs/windows/w40/evidence/inno-setup-license.txt') $licenses
$sourceUrl = "https://github.com/JendaNDT/rocker/tree/$commit"
@"
ROCKER — WINDOWS

Spusť instalační soubor Rocker-Windows-Setup a potom Rocker z nabídky Start.
Pokud používáš přenosný ZIP, zvol Extrahovat vše a spusť Rocker.Probe.exe.
Vývojové nástroje ani stahování zdrojů nejsou potřeba.
Vyžaduje Windows 11 x64 od 24H2. .NET a Windows App SDK jsou součástí aplikace.

Průvodce nastaví profil kytary, převod webových dat a zvukovou kartu.
Pro M-Audio M-Track Solo nainstaluj oficiální ovladač od výrobce, pokud není
nainstalovaný. Vyber tuto kartu, její výstup a jediný vstup s kytarou.
Sluchátka připoj ke stejné kartě. Monitoring se zapíná výslovně v Live;
začni nízkou hlasitostí. Přehrávání, efekty, metronom, záznam i looper používají
společné nativní jádro. Skutečnou odezvu a stabilní buffer ověř při hře.

Lekce, ladička, trenažéry, knihovna, noty, nahrávky, Looper, Studio, plány,
profil a statistiky fungují místně. Převod původní PWA je v Zálohy a soubory.
Původní webová data se nemažou. Vytvářej zálohy včetně nahrávek a projektů.
Noty mají vlastní syntetický strunný/bicí zvuk; není to plná banka GM nástrojů.
YouTube používá oficiální přehrávač a vyžaduje internet a WebView2 runtime.
AI je volitelná, vyžaduje vlastní klíč OpenAI; uložený Gemini klíč se nepoužije
u jiného poskytovatele. Čistě osobní Gemini cesta je v této verzi blokovaná
podle aktuálních podmínek poskytovatele. Žádná zvuková nahrávka se AI neposílá.

Odinstalace ponechá data v %LOCALAPPDATA%\Rocker. Před aktualizací zavři Rocker;
instalátor jej nebude ukončovat uprostřed nahrávání. Předchozí verze a záloha
zůstávají pro návrat; novější schéma se starší verzí neotevře naslepo.
Balík není podepsaný certifikátem. Smart App Control může jeho DLL blokovat;
kompatibilita se zapnutým Smart App Control není potvrzená.
Automatické kontroly nejsou potvrzením poslechu, online AI/YouTube,
Narratoru ani skutečné odezvy na M-Track. Ty patří do závěrečné přejímky.

Verze zdrojů: $commit
Zdrojový repozitář: $sourceUrl
Vedle tohoto balíku je samostatný Rocker-Windows-sources.zip s odpovídajícími
zdroji Rockeru, JUCE, AlphaTab, AlphaSkia a postupem sestavení. Pro spuštění jej nepotřebuješ.
Licence a oznámení jsou ve složce licenses; vlastní Windows kód je pod AGPLv3.
"@ | Set-Content (Join-Path $app 'CTI-ME.txt') -Encoding utf8BOM
@{
    commit = $commit
    juceCommit = $juceCommit
    alphaTabCommit = $notationSources[0].Commit
    alphaSkiaCommit = $notationSources[1].Commit
    checkedNotation = (Get-Content (Join-Path $reports 'notation-checks.json') -Raw | ConvertFrom-Json)
    checkedMedia = (Get-Content (Join-Path $reports 'media-checks.json') -Raw | ConvertFrom-Json)
    checkedStorage = (Get-Content (Join-Path $reports 'storage-checks.json') -Raw | ConvertFrom-Json)
    checkedPersistence = (Get-Content (Join-Path $reports 'persistence-view.json') -Raw | ConvertFrom-Json)
    checkedDomain = (Get-Content (Join-Path $reports 'domain-checks.json') -Raw | ConvertFrom-Json)
    checkedTransport = (Get-Content (Join-Path $reports 'transport-checks.json') -Raw | ConvertFrom-Json)
    checkedVideoBridge = (Get-Content (Join-Path $reports 'video-bridge.json') -Raw | ConvertFrom-Json)
    signalsmithStretchCommit = $mediaSources[0].Commit
    signalsmithLinearCommit = $mediaSources[1].Commit
    libwebmCommit = $mediaSources[2].Commit
    runUrl = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    minimumOs = 'Windows 11 24H2 x64'
    checkedWindow = $smoke
    toolchain = (Get-Content (Join-Path $reports 'toolchain.json') -Raw | ConvertFrom-Json)
} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $app 'build-info.json')

& (Join-Path $PSScriptRoot 'license-audit.ps1') -ApplicationDirectory $app -AssetsFile (Join-Path $PSScriptRoot 'Rocker.Probe/obj/project.assets.json')

# Sources are offered separately; Git archives exclude credentials and build caches.
Invoke-Git @('-C', $repository, 'archive', '--format=zip', "--output=$(Join-Path $sources 'rocker-source.zip')", 'HEAD')
Invoke-Git @('-C', $juce, 'archive', '--format=zip', "--output=$(Join-Path $sources 'JUCE-source.zip')", 'HEAD')
foreach ($dependency in $notationSources) {
    $checkout = Join-Path $stage ($dependency.Name + '-checkout')
    Invoke-Git @('clone', '--depth', '1', '--branch', $dependency.Tag, $dependency.Repository, $checkout) | Out-Null
    $revision = Invoke-Git @('-C', $checkout, 'rev-parse', 'HEAD')
    if ($revision -ne $dependency.Commit) { throw "$($dependency.Name) source revision mismatch." }
    Invoke-Git @('-C', $checkout, 'archive', '--format=zip', "--output=$(Join-Path $sources ($dependency.Name + '-source.zip'))", 'HEAD')
}
@"
Corresponding sources for Rocker commit $commit and JUCE $juceCommit.
Unpack rocker-source.zip into rocker and JUCE-source.zip into JUCE next to it.
Use x64 Developer PowerShell with the tools listed in rocker/windows/README.md.
Run from rocker/windows: ./build.ps1 -JuceSource ../../JUCE
NuGet restores the .NET/Windows SDK packages; exact observed tools are in build-info.json.
The complete JUCE source includes its third-party sources and original notices.
AlphaTab-source.zip and AlphaSkia-source.zip contain the unmodified upstream revisions
recorded in build-info.json. Their upstream build instructions are in each archive.
Rocker uses the exact NuGet packages 1.8.3 and 3.4.135; it does not rebuild those packages.
Signalsmith-Stretch-source.zip and Signalsmith-Linear-source.zip contain the pinned
MIT sources used by the common media player. CMake fetches these exact
commits automatically; local source overrides are documented in docs/windows/w04/media.md.
libwebm-source.zip contains the exact parser revision with its BSD notice and patent grant.
The WebM decoder uses the Opus implementation already included in the full JUCE sources.
MP4/AAC decoding uses optional system Media Foundation; no Windows codec DLL is bundled.
"@ | Set-Content (Join-Path $sources 'BUILD-SOURCES.txt')
Copy-Item (Join-Path $app 'build-info.json') $sources
[xml]$properties=Get-Content (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw
& (Join-Path $PSScriptRoot 'installer/build.ps1') -ApplicationDirectory $app -PackageDirectory $packages -Version $properties.Project.PropertyGroup.VersionPrefix -Revision $commit
foreach ($item in @(@{ Name = 'Rocker-Windows-x64.zip'; Directory = $app },
                    @{ Name = 'Rocker-Windows-sources.zip'; Directory = $sources })) {
    $destination = Join-Path $packages $item.Name
    [IO.Compression.ZipFile]::CreateFromDirectory($item.Directory, $destination, [IO.Compression.CompressionLevel]::Optimal, $false)
}
Get-ChildItem $packages -File | Where-Object {$_.Extension -in @('.zip','.exe')} | Sort-Object Name | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)"
} | Set-Content (Join-Path $packages 'SHA256SUMS.txt')
Write-Output "Runnable and separate source ZIPs created in $packages."
