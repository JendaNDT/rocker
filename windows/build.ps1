# SPDX-License-Identifier: AGPL-3.0-only
# Run in the x64 Developer PowerShell for Visual Studio selected in W02.
[CmdletBinding()]
param([switch]$Run, [string]$JuceSource = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Tento skript sestavuje Windows aplikaci a vyžaduje Windows.'
}
foreach ($tool in @('cl.exe', 'cmake', 'ninja', 'dotnet', 'git', 'node')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Chybí nástroj: $tool" }
}
function Invoke-Checked([string]$Tool, [string[]]$Arguments) {
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Tool skončil chybou $LASTEXITCODE." }
}
$nativeBuild = Join-Path $PSScriptRoot 'out/native'
$probeOutput = Join-Path $PSScriptRoot 'out/probe'
$reportDirectory = Join-Path $PSScriptRoot 'out/reports'
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
Push-Location $PSScriptRoot
try {
    $properties = [xml](Get-Content (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw)
    $version = $properties.Project.PropertyGroup.VersionPrefix
    $manifest = [xml](Get-Content (Join-Path $PSScriptRoot 'Rocker.Probe/app.manifest') -Raw)
    if ($manifest.assembly.assemblyIdentity.version -ne "$version.0") { throw 'The application manifest and product version differ.' }
    Invoke-Checked 'dotnet' @('restore', 'Rocker.slnx', '--locked-mode')
    # global.json verifies the selected SDK; Ninja avoids generator/version coupling to VS.
    Invoke-Checked 'dotnet' @('--info')
    Invoke-Checked 'node' @('../scripts/export-windows-content.mjs', '--check-frozen')
    Invoke-Checked 'node' @('../scripts/export-windows-migration-fixture.mjs', '--check')
    Invoke-Checked 'node' @('../scripts/export-windows-domain-reference.mjs', '--check-source')
    Invoke-Checked 'node' @('--test', 'Rocker.Probe/Video/player.contracts.cjs')
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Video.Checks', '-c', 'Release', '--no-restore')
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Domain.Checks', '-c', 'Release', '--no-restore', '--',
        (Join-Path $PSScriptRoot '../docs/windows/baseline/fixtures/domain-cases.json'),
        (Join-Path $PSScriptRoot 'content/catalog.json'), (Join-Path $reportDirectory 'domain-checks.json'))
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Infrastructure.Checks', '-c', 'Release', '--no-restore', '--', (Join-Path $reportDirectory 'storage-checks.json'))
    foreach ($browserKind in @('ui','bookmark')) {
        Invoke-Checked 'dotnet' @('run','--project','Rocker.Infrastructure.Checks','-c','Release','--no-restore','--','--browser-migration', (Join-Path $PSScriptRoot "../docs/windows/w37/fixtures/$browserKind.rockermigration"), (Join-Path $reportDirectory "browser-$browserKind-import.json"))
    }
    @{
        os = [Environment]::OSVersion.VersionString
        cl = (Get-Command cl.exe).Source
        msvcFileVersion = (Get-Item (Get-Command cl.exe).Source).VersionInfo.FileVersion
        windowsSdk = $env:WindowsSDKVersion
        cmake = (& cmake --version | Select-Object -First 1)
        ninja = (& ninja --version)
        dotnet = (& dotnet --version)
        visualStudio = $env:VisualStudioVersion
        productVersion = $version
        restoreMode = 'locked'
        checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content (Join-Path $reportDirectory 'toolchain.json')
    $configure = @('-S', '.', '-B', $nativeBuild, '-G', 'Ninja',
        '-DCMAKE_BUILD_TYPE=Release', '-DCMAKE_C_COMPILER=cl', '-DCMAKE_CXX_COMPILER=cl', '-DROCKER_WITH_JUCE=ON')
    if ($JuceSource) { $configure += "-DFETCHCONTENT_SOURCE_DIR_JUCE=$((Resolve-Path $JuceSource).Path)" }
    Invoke-Checked 'cmake' $configure
    Invoke-Checked 'cmake' @('--build', $nativeBuild, '--parallel', '2')
    Invoke-Checked 'ctest' @('--test-dir', $nativeBuild, '--output-on-failure',
        '--output-junit', (Join-Path $reportDirectory 'native-tests.xml'))
    $mediaReport = Join-Path $nativeBuild 'media-checks.json'
    if (-not (Test-Path $mediaReport)) { throw 'Native media checks did not produce evidence.' }
    $mediaResult = Get-Content $mediaReport -Raw | ConvertFrom-Json
    if ($mediaResult.result -ne 'passed' -or $mediaResult.checks -le 0) { throw 'Native media checks failed.' }
    Copy-Item $mediaReport (Join-Path $reportDirectory 'media-checks.json')
    $transportReport = Join-Path $nativeBuild 'transport-checks.json'
    if (-not (Test-Path $transportReport)) { throw 'Common transport checks did not produce evidence.' }
    $transportResult = Get-Content $transportReport -Raw | ConvertFrom-Json
    if ($transportResult.result -ne 'passed' -or $transportResult.checks -le 0) { throw 'Common transport checks failed.' }
    Copy-Item $transportReport (Join-Path $reportDirectory 'transport-checks.json')
    $mediaDependencies = & dumpbin /dependents (Join-Path $nativeBuild 'rocker_media_tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect native media imports.' }
    if ($mediaDependencies -match '^\s*(mfplat|mfreadwrite|mf)\.dll\s*$') { throw 'Optional media runtime is eagerly imported.' }
    $mediaDependencies | Set-Content (Join-Path $reportDirectory 'media-dependencies.txt')
    $audioDependencies = & dumpbin /dependents (Join-Path $nativeBuild 'rocker_audio.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect delivered native audio imports.' }
    if ($audioDependencies -match '^\s*(mfplat|mfreadwrite|mf)\.dll\s*$') { throw 'Optional media runtime is eagerly imported by the device DLL.' }
    $audioDependencies | Set-Content (Join-Path $reportDirectory 'audio-dependencies.txt')
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Interop.Checks', '-c', 'Release', '--no-restore', '--',
        (Join-Path $nativeBuild 'rocker_audio_test.dll'))
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Interop.Checks', '-c', 'Release', '--no-build', '--',
        (Join-Path $nativeBuild 'rocker_audio_test_incompatible.dll'), '--mismatched-abi')
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Notation.Checks', '-c', 'Release', '--no-restore', '--',
        (Join-Path $PSScriptRoot 'fixtures'), $reportDirectory)
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Interop.Checks', '-c', 'Release', '--no-build', '--',
        (Join-Path $nativeBuild 'rocker_audio_test.dll'), '--media', (Split-Path $PSScriptRoot -Parent))
    Invoke-Checked 'dotnet' @('publish', 'Rocker.Probe', '-c', 'Release', '-r', 'win-x64', '--no-restore',
        '-o', $probeOutput, "-p:RockerNativeDll=$(Join-Path $nativeBuild 'rocker_audio.dll')")
    $sourceCommit = (& git -C (Split-Path $PSScriptRoot -Parent) rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'Cannot determine source commit for diagnostics.' }
    @{ schemaVersion=1; sourceCommit=$sourceCommit; productVersion=$version; builtAtUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content (Join-Path $probeOutput 'app-build.json')
    Write-Output "Prototyp byl sestaven do $probeOutput. Ruční přejímka W03 zůstává samostatná."
    if ($Run) { & (Join-Path $probeOutput 'Rocker.Probe.exe') }
}
finally { Pop-Location }
