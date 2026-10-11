# SPDX-License-Identifier: AGPL-3.0-only
# CI prerequisite only. The final installer prerequisite is handled in W41.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Windows is required.' }
$locations = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/EdgeWebView/Application/*/msedgewebview2.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft/EdgeWebView/Application/*/msedgewebview2.exe')
)
$installed = @($locations | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue })
if ($installed.Count -gt 0) {
    Write-Output "WebView2 runtime present: $($installed[0].VersionInfo.FileVersion)"
    return
}
$bootstrap = Join-Path $env:RUNNER_TEMP 'MicrosoftEdgeWebview2Setup.exe'
if (-not $env:RUNNER_TEMP) { throw 'This automatic prerequisite installer is intended for the CI runner.' }
Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrap
$signature = Get-AuthenticodeSignature $bootstrap
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'The official WebView2 bootstrapper signature is not valid.'
}
$process = Start-Process $bootstrap -ArgumentList '/silent', '/install' -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "WebView2 runtime installation failed: $($process.ExitCode)" }
Write-Output 'The signed Microsoft WebView2 prerequisite installer completed.'
