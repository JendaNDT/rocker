# SPDX-License-Identifier: AGPL-3.0-only
# Exercises the published WinUI executable, without starting sound.
[CmdletBinding()]
param([string]$PublishDirectory = (Join-Path $PSScriptRoot 'out/probe'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$reportDirectory = Join-Path $PSScriptRoot 'out/reports'
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$executable = Join-Path $PublishDirectory 'Rocker.Probe.exe'
if (-not (Test-Path $executable)) { throw "Missing published executable: $executable" }
if (Get-ChildItem $PublishDirectory -Filter 'rocker_audio_test*' -Recurse) {
    throw 'A test audio backend must not be included in the application.'
}
$process = $null
$started = Get-Date
$previousDiagnostics = $env:ROCKER_DIAGNOSTICS_DIR
$env:ROCKER_DIAGNOSTICS_DIR = $reportDirectory
$notationReport = Join-Path $reportDirectory 'notation-view.json'
if (Test-Path $notationReport) { Remove-Item $notationReport }
try {
    $process = Start-Process -FilePath $executable -WorkingDirectory $PublishDirectory -PassThru
    $deadline = $started.AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Application exited before showing a window: $($process.ExitCode)" }
    } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw 'Application did not show a window within 45 seconds.' }
    $title = $process.MainWindowTitle
    if ($title -notlike 'Rocker*') { throw "Unexpected application window: $title" }
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw 'Application exited before native notation appeared.' }
    } while (-not (Test-Path $notationReport) -and (Get-Date) -lt $deadline)
    if (-not (Test-Path $notationReport)) { throw 'Native notation did not finish within 45 seconds.' }
    $notation = Get-Content $notationReport -Raw | ConvertFrom-Json
    if ($notation.result -ne 'passed' -or -not $notation.bitmapDecoded -or $notation.BarCount -ne 2) {
        throw "Published native notation failed: $(Get-Content $notationReport -Raw)"
    }
    if (-not $process.CloseMainWindow()) { throw 'The application rejected a normal window close.' }
    if (-not $process.WaitForExit(15000)) { throw 'The application did not exit after closing its window.' }
    if ($process.ExitCode -ne 0) { throw "Application exited with code $($process.ExitCode)." }
    @{
        result = 'passed'
        windowTitle = $title
        exitCode = $process.ExitCode
        audioPlaybackVerified = $false
        nativeNotationVerified = $true
        checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content (Join-Path $reportDirectory 'window-smoke.json')
    Write-Output 'PASS: published WinUI application opened a window and exited normally; audio playback not tested.'
}
catch {
    $_ | Out-String | Set-Content (Join-Path $reportDirectory 'window-smoke-error.txt')
    Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $started } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -in @('.NET Runtime', 'Application Error', 'Windows Error Reporting') } |
        Select-Object TimeCreated, ProviderName, Message |
        Format-List | Out-String | Set-Content (Join-Path $reportDirectory 'application-events.txt')
    throw
}
finally {
    $env:ROCKER_DIAGNOSTICS_DIR = $previousDiagnostics
    if ($null -ne $process) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
}
