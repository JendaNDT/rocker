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
$previousUserData = $env:ROCKER_USER_DATA_DIR
$env:ROCKER_USER_DATA_DIR = Join-Path $reportDirectory "isolated-shell-data"
$env:ROCKER_DIAGNOSTICS_DIR = $reportDirectory
$notationReport = Join-Path $reportDirectory 'notation-view.json'
$videoReport = Join-Path $reportDirectory 'video-bridge.json'
$shellReport = Join-Path $reportDirectory 'shell-navigation.json'
if (Test-Path $notationReport) { Remove-Item $notationReport }
if (Test-Path $videoReport) { Remove-Item $videoReport }
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
    if ($notation.result -ne 'passed' -or -not $notation.bitmapDecoded -or $notation.BarCount -ne 2 -or
        -not $notation.midiPreparedInCommonEngine) {
        throw "Published native notation failed: $(Get-Content $notationReport -Raw)"
    }
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw 'Application exited before the local video bridge appeared.' }
    } while (-not (Test-Path $videoReport) -and (Get-Date) -lt $deadline)
    if (-not (Test-Path $videoReport)) { throw 'Local WebView2 bridge did not initialize within 45 seconds.' }
    $video = Get-Content $videoReport -Raw | ConvertFrom-Json
    if ($video.result -ne 'passed' -or -not $video.localBridgeVerified -or -not $video.runtimeVersion) {
        throw "Local WebView2 bridge failed: $(Get-Content $videoReport -Raw)"
    }
    if (-not (Test-Path $shellReport)) { throw 'Native shell navigation report is missing.' }
    $shell = Get-Content $shellReport -Raw | ConvertFrom-Json
    if ($shell.result -ne 'passed' -or $shell.viewsVisited -ne 32 -or -not $shell.backRestoredTask -or $shell.audioHostsCreated -ne 1) {
        throw "Native shell navigation failed: $(Get-Content $shellReport -Raw)"
    }
    & (Join-Path $PSScriptRoot 'capture-window.ps1') -WindowHandle $process.MainWindowHandle.ToInt64() -Path (Join-Path $reportDirectory 'native-shell.png')
    if (-not $process.CloseMainWindow()) { throw 'The application rejected a normal window close.' }
    if (-not $process.WaitForExit(15000)) { throw 'The application did not exit after closing its window.' }
    if ($process.ExitCode -ne 0) { throw "Application exited with code $($process.ExitCode)." }
    @{
        result = 'passed'
        windowTitle = $title
        exitCode = $process.ExitCode
        audioPlaybackVerified = $false
        nativeNotationVerified = $true
        nativeShellViewsVisited = $shell.viewsVisited
        audioHostsCreated = $shell.audioHostsCreated
        midiPreparedInCommonEngine = $true
        localVideoBridgeVerified = $true
        webViewRuntime = $video.runtimeVersion
        onlineVideoVerified = $false
        audioCoexistenceVerified = $false
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
    $env:ROCKER_USER_DATA_DIR = $previousUserData
    if ($null -ne $process) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
}
