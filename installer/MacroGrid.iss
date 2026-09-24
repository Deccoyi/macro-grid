; Inno Setup script for the Macro Grid server. Built by installer\build-installer.ps1, which passes
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

#define AppName "Macro Grid"
#define AppExe "MacroGrid.exe"

[Setup]
; Never change AppId: it is how upgrades and the uninstaller find this app.
AppId={{6B0B0F6E-5C2B-4D53-9E0A-3D4B7B7A6C11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Macro Grid
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
; Inno's default entry name is "<name> version <version>"; Windows already shows the version in its own column.
UninstallDisplayName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=MacroGrid-Setup-{#AppVersion}
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
Name: "autostart"; Description: "Start Macro Grid when I sign in to Windows"; Flags: unchecked

[Files]
; The publish folder already contains LICENSE, THIRD_PARTY_NOTICES.md and the licenses folder; the wildcard below installs them next to the exe.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; The editor runs in a WebView2 window. Windows 11 and up-to-date Windows 10 have the runtime, older or stripped installs do not.
; Microsoft's small "Evergreen bootstrapper" is bundled (downloaded and signature-checked by build-installer.ps1, not stored in
; git) and only runs when the runtime is missing. It needs an internet connection on that PC.
#ifdef WebView2Setup
Source: "{#WebView2Setup}"; DestDir: "{tmp}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: deleteafterinstall; Check: not WebView2Installed
#endif

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Per-user autostart, so it follows the person who chose it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MacroGrid"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
#ifdef WebView2Setup
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing the WebView2 Runtime (needs an internet connection)..."; Flags: waituntilterminated; Check: not WebView2Installed
#endif
; Phones connect over the local network on port 9820; allow it on private networks only.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""Macro Grid"" dir=in action=allow protocol=TCP localport=9820 profile=private program=""{app}\{#AppExe}"""; Flags: runhidden; StatusMsg: "Allowing phones on your private network..."
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

; Profiles, paired devices, plugins and logs live in %AppData%\MacroGrid and are deliberately left in place
; on uninstall, so a reinstall picks up where the user left off.

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Macro Grid"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"

[Code]
const
  WebView2ClientKey = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function WebView2VersionOk(const Root: Integer; const SubKey: string): Boolean;
var
  Version: string;
begin
  Result := RegQueryStringValue(Root, SubKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;

{ True when the Evergreen WebView2 Runtime is installed (machine-wide, 32-bit view, or per user). }
function WebView2Installed: Boolean;
begin
  Result := WebView2VersionOk(HKLM64, WebView2ClientKey)
         or WebView2VersionOk(HKLM32, WebView2ClientKey)
         or WebView2VersionOk(HKCU, WebView2ClientKey);
end;
