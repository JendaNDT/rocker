# SPDX-License-Identifier: AGPL-3.0-only
# Run in the x64 Developer PowerShell for Visual Studio selected in W02.
[CmdletBinding()]
param([switch]$Run)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Tento skript sestavuje Windows aplikaci a vyžaduje Windows.'
}
foreach ($tool in @('cl.exe', 'cmake', 'ninja', 'dotnet', 'git')) {
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
    # global.json verifies the selected SDK; Ninja avoids generator/version coupling to VS.
    Invoke-Checked 'dotnet' @('--info')
    @{
        os = [Environment]::OSVersion.VersionString
        cl = (Get-Command cl.exe).Source
        msvcFileVersion = (Get-Item (Get-Command cl.exe).Source).VersionInfo.FileVersion
        windowsSdk = $env:WindowsSDKVersion
        cmake = (& cmake --version | Select-Object -First 1)
        ninja = (& ninja --version)
        dotnet = (& dotnet --version)
        visualStudio = $env:VisualStudioVersion
        checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content (Join-Path $reportDirectory 'toolchain.json')
    Invoke-Checked 'cmake' @('-S', '.', '-B', $nativeBuild, '-G', 'Ninja',
        '-DCMAKE_BUILD_TYPE=Release', '-DCMAKE_C_COMPILER=cl', '-DCMAKE_CXX_COMPILER=cl', '-DROCKER_WITH_JUCE=ON')
    Invoke-Checked 'cmake' @('--build', $nativeBuild, '--parallel', '2')
    Invoke-Checked 'ctest' @('--test-dir', $nativeBuild, '--output-on-failure',
        '--output-junit', (Join-Path $reportDirectory 'native-tests.xml'))
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Interop.Checks', '-c', 'Release', '--',
        (Join-Path $nativeBuild 'rocker_audio_test.dll'))
    Invoke-Checked 'dotnet' @('run', '--project', 'Rocker.Interop.Checks', '-c', 'Release', '--no-build', '--',
        (Join-Path $nativeBuild 'rocker_audio_test_incompatible.dll'), '--mismatched-abi')
    Invoke-Checked 'dotnet' @('publish', 'Rocker.Probe', '-c', 'Release', '-r', 'win-x64',
        '-o', $probeOutput, "-p:RockerNativeDll=$(Join-Path $nativeBuild 'rocker_audio.dll')")
    Write-Output "Prototyp byl sestaven do $probeOutput. Ruční přejímka W03 zůstává samostatná."
    if ($Run) { & (Join-Path $probeOutput 'Rocker.Probe.exe') }
}
finally { Pop-Location }
