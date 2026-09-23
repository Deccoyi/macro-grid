; Inno Setup script for the Macro Station server. Built by installer\build-installer.ps1, which passes
; AppVersion, SourceDir (the publish folder) and OutputDir.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\server"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "Macro Station"
#define AppExe "MacroStation.exe"

[Setup]
; Never change AppId: it is how upgrades and the uninstaller find this app.
AppId={{6B0B0F6E-5C2B-4D53-9E0A-3D4B7B7A6C11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Macro Station
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir={#OutputDir}
OutputBaseFilename=MacroStation-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
; Close a running server before replacing its files.
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: "autostart"; Description: "Start Macro Station when I sign in to Windows"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Per-user autostart, so it follows the person who chose it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MacroStation"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
; Phones connect over the local network on port 9820; allow it on private networks only.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""Macro Station"" dir=in action=allow protocol=TCP localport=9820 profile=private program=""{app}\{#AppExe}"""; Flags: runhidden; StatusMsg: "Allowing phones on your private network..."
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Macro Station"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"

; Profiles, paired devices, plugins and logs live in %AppData%\MacroStation and are deliberately left in place
; on uninstall, so a reinstall picks up where the user left off.
