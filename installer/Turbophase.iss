; Turbophase Inno Setup Script
; This script creates a Windows installer for Turbophrase

#ifndef Version
  #define Version "1.0.0"
#endif

#ifndef Architecture
  #define Architecture "x64"
#endif

#ifndef SourcePath
  #define SourcePath "..\publish"
#endif

[Setup]
; Application info
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName=Turbophrase
AppVersion={#Version}
AppVerName=Turbophrase {#Version}
AppPublisher=Moaid Hathot
AppPublisherURL=https://github.com/MoaidHathot/Turbophrase
AppSupportURL=https://github.com/MoaidHathot/Turbophrase/issues
AppUpdatesURL=https://github.com/MoaidHathot/Turbophrase/releases
DefaultDirName={autopf}\Turbophrase
DefaultGroupName=Turbophrase
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts
OutputBaseFilename=Turbophrase-{#Version}-{#Architecture}-setup
SetupIconFile=..\src\Turbophrase\Resources\Turbophrase.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Architecture settings
#if Architecture == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

; Version info
VersionInfoVersion={#Version}
VersionInfoCompany=Moaid Hathot
VersionInfoDescription=AI-powered text transformation tool for Windows
VersionInfoCopyright=Copyright 2026 Moaid Hathot
VersionInfoProductName=Turbophrase
VersionInfoProductVersion={#Version}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start Turbophrase when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Turbophrase"; Filename: "{app}\Turbophrase.exe"
Name: "{group}\{cm:UninstallProgram,Turbophrase}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Turbophrase"; Filename: "{app}\Turbophrase.exe"; Tasks: desktopicon

[Registry]
; Add to startup if selected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Turbophrase"; ValueData: """{app}\Turbophrase.exe"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\Turbophrase.exe"; Description: "{cm:LaunchProgram,Turbophrase}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Clean up user data if desired (optional, commented out by default)
    // DelTree(ExpandConstant('{localappdata}\Turbophrase'), True, True, True);
  end;
end;
