# SPDX-License-Identifier: AGPL-3.0-only
# Exercises the published WinUI executable, without starting sound.
[CmdletBinding()]
param([string]$PublishDirectory = (Join-Path $PSScriptRoot 'out/probe'),
      [ValidateSet('home','components')][string]$WorkspaceView = 'home',
      [string]$ReportDirectory = (Join-Path $PSScriptRoot 'out/reports'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$reportDirectory = $ReportDirectory
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$executable = Join-Path $PublishDirectory 'Rocker.Probe.exe'
if (-not (Test-Path $executable)) { throw "Missing published executable: $executable" }
if (Get-ChildItem $PublishDirectory -Filter 'rocker_audio_test*' -Recurse) {
    throw 'A test audio backend must not be included in the application.'
}
$process = $null
$started = Get-Date
$previousView = $env:ROCKER_DIAGNOSTIC_VIEW
$env:ROCKER_DIAGNOSTIC_VIEW = $WorkspaceView
$previousDiagnostics = $env:ROCKER_DIAGNOSTICS_DIR
$previousUserData = $env:ROCKER_USER_DATA_DIR
$env:ROCKER_USER_DATA_DIR = Join-Path $reportDirectory "isolated-shell-data"
$env:ROCKER_DIAGNOSTICS_DIR = $reportDirectory
$notationReport = Join-Path $reportDirectory 'notation-view.json'
$videoReport = Join-Path $reportDirectory 'video-bridge.json'
$shellReport = Join-Path $reportDirectory 'shell-navigation.json'
$persistenceReport = Join-Path $reportDirectory 'persistence-view.json'
$componentsReport = Join-Path $reportDirectory 'native-components.json'
foreach ($stale in @($shellReport,$componentsReport,$persistenceReport)) { if (Test-Path $stale) { Remove-Item $stale } }
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
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw 'Application exited before native shell checks finished.' }
    } while (-not (Test-Path $shellReport) -and (Get-Date) -lt $deadline)
    if (-not (Test-Path $shellReport)) { throw 'Native shell navigation report is missing.' }
    $shell = Get-Content $shellReport -Raw | ConvertFrom-Json
    if ($shell.result -ne 'passed' -or $shell.viewsVisited -ne 32 -or -not $shell.backRestoredTask -or $shell.audioHostsCreated -ne 1) {
        throw "Native shell navigation failed: $(Get-Content $shellReport -Raw)"
    }
    if (-not (Test-Path $persistenceReport)) { throw 'Persistence report is missing.' }
    $persistence = Get-Content $persistenceReport -Raw | ConvertFrom-Json
    if ($persistence.result -ne 'passed' -or -not $persistence.reopenedDatabase) { throw 'Native persistence did not survive database reopen.' }
    if (-not (Test-Path $componentsReport)) { throw 'Native components report is missing.' }
    $components = Get-Content $componentsReport -Raw | ConvertFrom-Json
    if ($components.result -ne 'passed' -or $components.checks -lt 31) { throw 'Native components failed.' }
    foreach ($audioView in @('amplifier-view.json','metronome-view.json')) {
        $viewPath = Join-Path $reportDirectory $audioView
        if (-not (Test-Path $viewPath)) { throw "Missing audio view report: $audioView" }
        $view = Get-Content $viewPath -Raw | ConvertFrom-Json
        if ($view.result -ne 'passed' -or -not $view.nativeParametersVerified -or -not $view.persistenceReopened) {
            throw "Native audio form/persistence failed: $audioView"
        }
    }
    $tunerPath = Join-Path $reportDirectory 'tuner-view.json'
    if (-not (Test-Path $tunerPath)) { throw 'Missing native tuner report.' }
    $tuner = Get-Content $tunerPath -Raw | ConvertFrom-Json
    if ($tuner.result -ne 'passed' -or $tuner.sixStringControls -ne 6 -or -not $tuner.persistenceReopened -or -not $tuner.noInputInventsNoNote) { throw 'Native tuner/persistence failed.' }
    $surgicalPath = Join-Path $reportDirectory 'surgical-view.json'
    if (-not (Test-Path $surgicalPath)) { throw 'Native surgical trainer report is missing.' }
    $surgical = Get-Content $surgicalPath -Raw | ConvertFrom-Json
    if ($surgical.result -ne 'passed' -or $surgical.cleanRepeats -ne 3 -or -not $surgical.nativeNoteList) { throw "Native surgical trainer failed: $(Get-Content $surgicalPath -Raw)" }
    $lessonsPath = Join-Path $reportDirectory 'lessons-view.json'
    if (-not (Test-Path $lessonsPath)) { throw 'Native lesson verification report is missing.' }
    $lessons = Get-Content $lessonsPath -Raw | ConvertFrom-Json
    if ($lessons.result -ne 'passed' -or $lessons.nativeStepKinds -ne 3 -or $lessons.totalLessons -ne 18 -or -not $lessons.completionSavedOnce) { throw "Native lesson verification failed: $(Get-Content $lessonsPath -Raw)" }
    $studioPath = Join-Path $reportDirectory 'studio-view.json'
    if (-not (Test-Path $studioPath)) { throw 'Studio native verification report is missing.' }
    $studio = Get-Content $studioPath -Raw | ConvertFrom-Json
    if ($studio.result -ne 'passed' -or $studio.nativeTrackControls -ne 2 -or $studio.timelineRegions -ne 2 -or $studio.exactFrames -ne 560 -or -not $studio.persistenceReopened) { throw "Studio form/project/source verification failed: $(Get-Content $studioPath -Raw)" }
    $looperPath = Join-Path $reportDirectory 'looper-view.json'
    if (-not (Test-Path $looperPath)) { throw 'Looper workspace report is missing.' }
    $looper = Get-Content $looperPath -Raw | ConvertFrom-Json
    if ($looper.result -ne 'passed' -or $looper.nativeLayersLoaded -ne 2 -or $looper.exactFrames -ne 480 -or -not $looper.persistenceReopened -or -not $looper.pausedWithoutAudioDevice) { throw 'Native Looper form or session persistence failed.' }
    $recordingsPath = Join-Path $reportDirectory 'recordings-view.json'
    if (-not (Test-Path $recordingsPath)) { throw 'Recording workspace report is missing.' }
    $recordings = Get-Content $recordingsPath -Raw | ConvertFrom-Json
    if ($recordings.result -ne 'passed' -or -not $recordings.persistenceReopened -or -not $recordings.nativeLibraryList -or $recordings.nativeRecordingControls -lt 1) { throw 'Recording library/persistence failed.' }
    $songsPath = Join-Path $reportDirectory 'songs-view.json'
    if (-not (Test-Path $songsPath)) { throw 'Song workspace report is missing.' }
    $songsView = Get-Content $songsPath -Raw | ConvertFrom-Json
    if ($songsView.result -ne 'passed' -or -not $songsView.persistenceReopened -or -not $songsView.nativeWaveformCanvas -or -not $songsView.missingSourcePreserved) { throw 'Song workspace persistence or controls failed.' }
    $diagnosticsPath = Join-Path $reportDirectory 'diagnostics-view.json'
    if (-not (Test-Path $diagnosticsPath)) { throw 'Missing native diagnostics report.' }
    $diagnostics = Get-Content $diagnosticsPath -Raw | ConvertFrom-Json
    if ($diagnostics.result -ne 'passed' -or -not $diagnostics.cachedCountersRead -or -not $diagnostics.unavailableDriverXrunsNotZero) { throw 'Diagnostic report/control contract failed.' }
    if (-not (Test-Path (Join-Path $reportDirectory 'native-content.png'))) { throw 'Native content capture is missing.' }
    try { & (Join-Path $PSScriptRoot 'capture-window.ps1') -WindowHandle $process.MainWindowHandle.ToInt64() -Path (Join-Path $reportDirectory 'native-shell.png') }
    catch { $_ | Out-String | Set-Content (Join-Path $reportDirectory 'desktop-capture-unavailable.txt') }
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
    $env:ROCKER_DIAGNOSTIC_VIEW = $previousView
    $env:ROCKER_DIAGNOSTICS_DIR = $previousDiagnostics
    $env:ROCKER_USER_DATA_DIR = $previousUserData
    if ($null -ne $process) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
}
