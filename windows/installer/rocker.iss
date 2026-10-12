; SPDX-License-Identifier: AGPL-3.0-only
#ifndef AppSource
  #error AppSource is required
#endif
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef Revision
  #error Revision is required
#endif
#ifndef PackageOutput
  #error PackageOutput is required
#endif
#define VersionFolder AppVersion + "-" + Copy(Revision, 1, 12)
[Setup]
AppId={{8E6BDC86-3624-4D36-A41E-557024BC647D}
AppName=Rocker
AppVersion={#AppVersion}
AppPublisher=JendaNDT
AppPublisherURL=https://github.com/JendaNDT/rocker
DefaultDirName={localappdata}\Programs\Rocker
DefaultGroupName=Rocker
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.26100
AppMutex=Local\JendaNDT.Rocker.SetupGuard
CloseApplications=no
RestartApplications=no
ChangesAssociations=yes
UninstallDisplayIcon={app}\versions\{#VersionFolder}\Rocker.Probe.exe
OutputDir={#PackageOutput}
OutputBaseFilename=Rocker-Windows-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
LicenseFile={#AppSource}\LICENSE.txt
[Languages]
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
[Messages]
ConfirmUninstall=Chceš odebrat Rocker?%n%nNahrávky, projekty, nastavení a zálohy zůstanou ve tvém účtu Windows.
[Files]
Source: "{#AppSource}\*"; DestDir: "{app}\versions\{#VersionFolder}"; Excludes: "LICENSE.txt"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppSource}\LICENSE.txt"; DestDir: "{app}\versions\{#VersionFolder}"; Flags: ignoreversion; AfterInstall: PrepareProfileUpdate
[Icons]
Name: "{userprograms}\Rocker\Rocker"; Filename: "{app}\versions\{#VersionFolder}\Rocker.Probe.exe"; WorkingDir: "{app}\versions\{#VersionFolder}"; AppUserModelID: "JendaNDT.Rocker"
Name: "{userprograms}\Rocker\Předchozí verze Rockeru"; Filename: "{code:PreviousExe}"; WorkingDir: "{code:PreviousFolder}"; Check: HasPrevious; AppUserModelID: "JendaNDT.Rocker"
[Registry]
Root: HKCU; Subkey: "Software\Classes\.rockerproj"; ValueType: string; ValueName: ""; ValueData: "JendaNDT.Rocker.Project"; Flags: createvalueifdoesntexist
Root: HKCU; Subkey: "Software\Classes\.rockerproj\OpenWithProgids"; ValueType: none; ValueName: "JendaNDT.Rocker.Project"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\JendaNDT.Rocker.Project"; ValueType: string; ValueName: ""; ValueData: "Projekt Rockeru"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\JendaNDT.Rocker.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\versions\{#VersionFolder}\Rocker.Probe.exe,0"
Root: HKCU; Subkey: "Software\Classes\JendaNDT.Rocker.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\versions\{#VersionFolder}\Rocker.Probe.exe"" ""%1"""
[Run]
Filename: "{app}\versions\{#VersionFolder}\Rocker.Probe.exe"; Description: "Spustit Rocker"; Flags: nowait postinstall skipifsilent
[Code]
const StateKey = 'Software\JendaNDT\Rocker';
var PreviousPath, PreviousVersion: String;
    BackupExitCode: Integer;
function PreviousExe(Param: String): String;
begin Result := PreviousPath + '\Rocker.Probe.exe'; end;
function PreviousFolder(Param: String): String;
begin Result := PreviousPath; end;
function HasPrevious: Boolean;
begin Result := (PreviousPath <> '') and FileExists(PreviousExe('')); end;
function VersionPart(var Value: String): Integer;
var P: Integer; Part: String;
begin P := Pos('.', Value); if P = 0 then begin Part := Value; Value := ''; end
else begin Part := Copy(Value, 1, P-1); Delete(Value, 1, P); end; Result := StrToIntDef(Part, 0); end;
function CompareVersion(A, B: String): Integer;
var I, Left, Right: Integer;
begin Result := 0; for I := 1 to 4 do begin Left := VersionPart(A); Right := VersionPart(B); if Left < Right then begin Result := -1; exit; end; if Left > Right then begin Result := 1; exit; end; end; end;
procedure InitializeWizard;
begin RegQueryStringValue(HKCU, StateKey, 'CurrentPath', PreviousPath); RegQueryStringValue(HKCU, StateKey, 'Version', PreviousVersion); end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin Result := '';
if PreviousPath = ExpandConstant('{app}\versions\{#VersionFolder}') then RegQueryStringValue(HKCU, StateKey, 'PreviousPath', PreviousPath);
if CompareVersion('{#AppVersion}', PreviousVersion) < 0 then begin
if WizardSilent then Result := 'Návrat starší verze spusť ručně. Použij zálohu kompatibilní s její verzí dat.'
else if MsgBox('Instaluješ starší verzi. Novější schéma dat nemusí umět otevřít. Máš kompatibilní zálohu a chceš pokračovat?', mbConfirmation, MB_YESNO) <> IDYES then Result := 'Návrat verze byl zrušen.'; end; end;
function UserDataRoot: String;
begin Result := GetEnv('ROCKER_USER_DATA_DIR'); if Result = '' then Result := ExpandConstant('{localappdata}\Rocker'); end;
procedure PrepareProfileUpdate;
begin
if FileExists(UserDataRoot + '\database\rocker.db') then begin
if not Exec(ExpandConstant('{app}\versions\{#VersionFolder}\Rocker.Probe.exe'), '--prepare-update', '', SW_HIDE, ewWaitUntilTerminated, BackupExitCode) or (BackupExitCode <> 0) then RaiseException('Záloha před aktualizací se nezdařila. Původní data a předchozí verze zůstávají zachované. Zkontroluj volné místo a místní profil.');
end;
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin if CurStep = ssPostInstall then begin
RegWriteStringValue(HKCU, StateKey, 'CurrentPath', ExpandConstant('{app}\versions\{#VersionFolder}'));
RegWriteStringValue(HKCU, StateKey, 'Version', '{#AppVersion}');
if HasPrevious then RegWriteStringValue(HKCU, StateKey, 'PreviousPath', PreviousPath);
end; end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Current: String;
begin if CurUninstallStep = usPostUninstall then begin
if RegQueryStringValue(HKCU, 'Software\Classes\.rockerproj', '', Current) and (Current = 'JendaNDT.Rocker.Project') then RegDeleteValue(HKCU, 'Software\Classes\.rockerproj', '');
RegDeleteKeyIncludingSubkeys(HKCU, StateKey); end; end;
