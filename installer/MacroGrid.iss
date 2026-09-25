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
; SHA-256 of license-agreement.txt, computed and passed in by build-installer.ps1. The license page is shown unless the person already
; accepted exactly this text (the [Registry] entry below records it); empty means "always show it" (a build made by hand).
#ifndef AgreementHash
  #define AgreementHash ""
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
; The person has to accept this text (no warranty, limitation of liability, MIT license) before anything is installed.
LicenseFile=license-agreement.txt
; Close a running server before replacing its files.
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"
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

[InstallDelete]
; The editor and deck files carry a content hash in their names, so without this every upgrade would leave the previous version's files next to the new ones.
Type: filesandordirs; Name: "{app}\wwwroot"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Per-user autostart, so it follows the person who chose it. The argument tells the app that Windows started it, so the
; "when Windows starts Macro Grid" preference applies (tray by default) instead of the "when I open it" one.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MacroGrid"; ValueData: """{app}\{#AppExe}"" --autostart"; Flags: uninsdeletevalue; Tasks: autostart

#if AgreementHash != ""
; The text this setup showed (or skipped because it was already accepted). Per person: HKCU is the account that approved the administrator prompt,
; normally the person themselves. Another administrator approving would leave a hash that does not match, so the page is shown again (too much, never too little).
Root: HKCU; Subkey: "Software\Macro Grid"; ValueType: string; ValueName: "AcceptedAgreement"; ValueData: "{#AgreementHash}"; Flags: uninsdeletevalue uninsdeletekeyifempty
#endif

[Run]
#ifdef WebView2Setup
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing the WebView2 Runtime (needs an internet connection)..."; Flags: waituntilterminated; Check: not WebView2Installed
#endif
; Phones connect over the local network on port 9820; allow it on private and domain (company) networks, never on public ones.
; The old rule goes first, so an upgrade replaces it instead of adding another copy of the same rule (no match is not an error).
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Macro Grid"""; Flags: runhidden; StatusMsg: "Allowing phones on your private network..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""Macro Grid"" dir=in action=allow protocol=TCP localport=9820 profile=domain,private program=""{app}\{#AppExe}"""; Flags: runhidden; StatusMsg: "Allowing phones on your private network..."
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent; Check: not IsUpdateRun
; After an automatic update (the app started this setup with /UPDATE) start it again as the person, not as administrator. The argument makes
; it show "Updated to <version>" once. The finished page closes by itself in that case, so the "Start" entry above is left out.
; runasoriginaluser only takes effect on a postinstall entry (without postinstall the app came back elevated, found by testing); a postinstall
; entry runs when the finished page is left, which the auto-close in CurPageChanged does.
Filename: "{app}\{#AppExe}"; Parameters: "--updated"; Description: "Start {#AppName}"; Flags: nowait postinstall runasoriginaluser; Check: IsUpdateRun

; Profiles, paired devices, plugins and logs live in %AppData%\MacroGrid and are deliberately left in place
; on uninstall, so a reinstall picks up where the user left off.

[UninstallRun]
; A running server locks its files, and the uninstaller would leave folders behind. Close it first.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "StopApp"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Macro Grid"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"

[UninstallDelete]
; Files the installer did not place itself (an older build let WebView2 write MacroGrid.exe.WebView2 next to the exe) would
; otherwise stay behind. The program folder holds no user data: profiles, devices, plugins and logs live in %AppData%\MacroGrid.
Type: filesandordirs; Name: "{app}"

[Code]
{ Close a running Macro Grid before files are replaced, so an upgrade does not stop on locked files or leave a half-installed folder. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

{ True when the app started this setup itself to update (the /UPDATE switch). The setup is visible (a progress window), not silent. }
function IsUpdateRun: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/UPDATE') = 0 then
      Result := True;
end;

{ The agreement text this person accepted earlier, as the hash the previous setup recorded in their HKCU; empty when there is none. }
function AcceptedAgreementHash: string;
begin
  if not RegQueryStringValue(HKCU, 'Software\Macro Grid', 'AcceptedAgreement', Result) then
    Result := '';
end;

{ A setup run by hand (a first install or a downloaded setup) always shows the agreement. An update (the /UPDATE switch) shows it only when its text
  changed since this person accepted it, and none of the other pages: administrator prompt, progress window, done. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if not IsUpdateRun then
    Exit;
  if PageID = wpLicense then
    Result := ('{#AgreementHash}' <> '') and (CompareText(AcceptedAgreementHash, '{#AgreementHash}') = 0)
  else
    Result := (PageID = wpWelcome) or (PageID = wpSelectDir) or (PageID = wpSelectProgramGroup)
           or (PageID = wpSelectTasks) or (PageID = wpReady);
end;

{ After an update the finished page has nothing to say: move on by itself, so the person sees the progress window and then the app comes back. }
procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and IsUpdateRun then
    WizardForm.NextButton.OnClick(WizardForm.NextButton);
end;

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
