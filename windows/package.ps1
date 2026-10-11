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
$expectedJuce = [regex]::Match((Get-Content (Join-Path $PSScriptRoot 'CMakeLists.txt') -Raw), 'GIT_TAG ([a-f0-9]{40})').Groups[1].Value
if ($juceCommit -ne $expectedJuce) { throw 'JUCE sources do not match the configured revision.' }
$smoke = Get-Content (Join-Path $reports 'window-smoke.json') -Raw | ConvertFrom-Json
if ($smoke.result -ne 'passed') { throw 'Window smoke test has not passed.' }
foreach ($required in @('Rocker.Probe.exe', 'rocker_audio.dll', 'Rocker.Probe.runtimeconfig.json')) {
    if (-not (Test-Path (Join-Path $publish $required))) { throw "Missing application file: $required" }
}
if (Get-ChildItem $publish -Filter 'rocker_audio_test*' -Recurse) { throw 'Test backend found in publish output.' }
New-Item -ItemType Directory -Force -Path $app, $sources, $packages | Out-Null
Copy-Item (Join-Path $publish '*') $app -Recurse
$licenses = Join-Path $app 'licenses'
New-Item -ItemType Directory -Force -Path $licenses | Out-Null
$evidence = Join-Path $repository 'docs/windows/decisions/evidence'
foreach ($name in @('AGPL-3.0-only.txt', 'GPL-3.0-only.txt', 'juce-license.txt', 'juce.spdx.json',
    'asio-license.txt', 'windowsapp-license.txt', 'webview-sdk-license.txt')) {
    Copy-Item (Join-Path $evidence $name) $licenses
}

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
$sourceUrl = "https://github.com/JendaNDT/rocker/tree/$commit"
@"
ROCKER - TESTOVACÍ PROTOTYP PRO WINDOWS

1. Rozbal celý ZIP do vlastní složky (Extrahovat vše).
2. Spusť Rocker.Probe.exe. Ostatní soubory ponech vedle něj.
3. Začni s nízkou hlasitostí a tlačítkem Spustit tón; Zastavit tón ukončí.

Vyžaduje Windows 11 x64 od 24H2. Vývojové nástroje nejsou potřeba.
Toto je první technický prototyp s testovacím tónem, ne kompletní Rocker.
Zvuk, skutečná odezva a chování na tvém zařízení vyžadují ruční ověření.
Balík není podepsaný certifikátem. Není potřeba vypínat ochrany Windows.

Verze zdrojů: $commit
Zdrojový repozitář: $sourceUrl
Vedle tohoto balíku je samostatný Rocker-Windows-sources.zip s odpovídajícími
zdroji Rockeru, JUCE a postupem sestavení. Pro spuštění jej nepotřebuješ.
Licence a oznámení jsou ve složce licenses; vlastní Windows kód je pod AGPLv3.
"@ | Set-Content (Join-Path $app 'CTI-ME.txt') -Encoding utf8BOM
@{
    commit = $commit
    juceCommit = $juceCommit
    runUrl = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    minimumOs = 'Windows 11 24H2 x64'
    checkedWindow = $smoke
    toolchain = (Get-Content (Join-Path $reports 'toolchain.json') -Raw | ConvertFrom-Json)
} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $app 'build-info.json')

# Sources are offered separately; Git archives exclude credentials and build caches.
Invoke-Git @('-C', $repository, 'archive', '--format=zip', "--output=$(Join-Path $sources 'rocker-source.zip')", 'HEAD')
Invoke-Git @('-C', $juce, 'archive', '--format=zip', "--output=$(Join-Path $sources 'JUCE-source.zip')", 'HEAD')
@"
Corresponding sources for Rocker commit $commit and JUCE $juceCommit.
Unpack rocker-source.zip into rocker and JUCE-source.zip into JUCE next to it.
Use x64 Developer PowerShell with the tools listed in rocker/windows/README.md.
Run from rocker/windows: ./build.ps1 -JuceSource ../../JUCE
NuGet restores the .NET/Windows SDK packages; exact observed tools are in build-info.json.
The complete JUCE source includes its third-party sources and original notices.
"@ | Set-Content (Join-Path $sources 'BUILD-SOURCES.txt')
Copy-Item (Join-Path $app 'build-info.json') $sources
foreach ($item in @(@{ Name = 'Rocker-Windows-x64.zip'; Directory = $app },
                    @{ Name = 'Rocker-Windows-sources.zip'; Directory = $sources })) {
    $destination = Join-Path $packages $item.Name
    [IO.Compression.ZipFile]::CreateFromDirectory($item.Directory, $destination, [IO.Compression.CompressionLevel]::Optimal, $false)
}
Get-ChildItem $packages -Filter '*.zip' | Sort-Object Name | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)"
} | Set-Content (Join-Path $packages 'SHA256SUMS.txt')
Write-Output "Runnable and separate source ZIPs created in $packages."
