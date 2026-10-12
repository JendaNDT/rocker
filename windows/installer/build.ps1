# SPDX-License-Identifier: AGPL-3.0-only
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ApplicationDirectory,
      [Parameter(Mandatory)][string]$PackageDirectory,
      [Parameter(Mandatory)][string]$Version,
      [Parameter(Mandatory)][string]$Revision)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$pin=Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$tools=Join-Path $PSScriptRoot '../out/installer-tools'
New-Item -ItemType Directory -Force $tools | Out-Null
$compiler=Join-Path $tools 'Inno/ISCC.exe'
if(-not (Test-Path $compiler)) {
    $download=Join-Path $tools 'inno-setup.exe'
    Invoke-WebRequest -Uri $pin.url -OutFile $download
    if((Get-FileHash $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pin.sha256){throw 'Pinned installer compiler download checksum mismatch.'}
    $process=Start-Process $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER',"/DIR=`"$(Join-Path $tools 'Inno')`"") -Wait -PassThru
    if($process.ExitCode -ne 0){throw 'Inno Setup compiler installation failed.'}
}
& $compiler "/DAppSource=$((Resolve-Path $ApplicationDirectory).Path)" "/DPackageOutput=$([IO.Path]::GetFullPath($PackageDirectory))" "/DAppVersion=$Version" "/DRevision=$Revision" (Join-Path $PSScriptRoot 'rocker.iss')
if($LASTEXITCODE -ne 0){throw 'Rocker installer compilation failed.'}
@{compilerVersion=$pin.version;compilerSource=$pin.url;compilerInstallerSha256=$pin.sha256;productVersion=$Version;revision=$Revision;perUser=$true;adminRequired=$false;preservesUserData=$true;unsigned=$true} | ConvertTo-Json | Set-Content (Join-Path $PackageDirectory 'installer-build.json')
