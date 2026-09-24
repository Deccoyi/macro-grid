# Automatic profile switching by the active window

A device can follow the foreground window on the server's computer: when a media player comes to the front, the phone switches to the
player's profile; when it closes, the phone goes back to the profile the user had picked. A **lock** in the phone's profile drawer pauses
the automatic switching, while switching by hand (the drawer or a `core.profile` button) always works. Each device opts in on its own.

## Behavior (one stack per session)

Every `ClientSession` holds an `AutoSwitchState`: a stack of entries `{ ProfileId, Source: Manual | Rule, ProcessName? }` and a `Locked` flag.

- **A window with a rule comes to the front** (the process name matches): its rule entry is moved to the top of the stack, or pushed if it is not
  there, and the device switches to that profile.
- **A window without a rule comes to the front (focus lost):** the profile follows focus, an open application is not enough. Every `Rule` entry
  is dropped and the device returns to the profile picked by hand (or the default). When the matching window comes back to the front the
  device switches to it again.
- **A window with a rule closes:** "closed" means the process has no visible top-level window left (a player hidden in the tray counts as
  closed). The dead `Rule` entries are dropped and the device returns to the entry below.
- **The stack becomes empty:** the device returns to the default profile: the profile assigned to the device (`PairedDevice.AssignedProfileId`),
  else `AppPreferences.DefaultProfileId`, else the first profile (`ProfileResolver.ResolveDefault`).
- **A manual pick** (drawer or `core.profile`) pushes a `Manual` entry. It acts as the base and is never dropped by process checks. If a
  window with a rule then comes to the front it goes on top; when that window closes the device returns to the manually picked profile. So
  "broadcast profile (by hand), player opens, player closes, broadcast profile" works naturally.
- **While locked:** window events are ignored, manual switches still work. Unlocking evaluates the current state once.
- **Opt-in:** a device with `PairedDevice.FollowActiveWindow = false` never takes part.

## Model and persistence

- `Profile.AppMatches` is a list of `AppMatch { ProcessName (for example "Player.exe"), TitleContains? }`. Rules live on the profile. If several
  profiles match the same window, the first one in `ProfileStore.All` wins. `ProfileValidator` rejects empty and duplicate rules.
- `PairedDevice.FollowActiveWindow` and `PairedDevice.AutoSwitchLocked` are stored in `devices.json`, so the lock survives a reconnect
  (`DeviceStore` setters follow the `AssignProfile` pattern).
- `AppPreferences.DefaultProfileId` is the app-wide default profile.

## Windows side

`MacroGrid.Windows/Windows/ForegroundWindowMonitor` is event driven (no polling): a thread with its own message loop calls
`SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT)`; on each event it reads the process name (`GetWindowThreadProcessId`) and the title
(`GetWindowText`) and raises `ForegroundChanged`. `HasVisibleWindow(processName)` uses `EnumWindows` with `IsWindowVisible`. Closing is detected by
pruning on every foreground event and, while a session has a `Rule` entry on its stack, by a two-second `PeriodicTimer` (no timer runs
when there is no rule on any stack). The Core only knows the `IActiveWindowSource` interface, which is what the tests fake.

## Core

`Sessions/AutoProfileSwitcher` (a hosted service) listens to `IActiveWindowSource`. For each event it goes through the sessions whose device follows
the window and is not locked, applies the stack logic (`AutoSwitchState.OnForeground / Prune / OnManual`, a pure class covered by unit tests), and
switches through `SessionDeviceController`. It sends nothing if the active profile already is the target. A manual switch goes through
`SwitchProfileAsync` (records a `Manual` entry); the switcher itself uses `ApplyProfileAsync` so a rule match is never recorded as a manual pick.

## Protocol

- Client to server: `profile.lock { locked }`. The server stores it in `DeviceStore`, and when unlocking evaluates the current state again.
- Server to client: `profiles.list` carries `autoSwitch: { enabled, locked }` and is sent again whenever either changes. Older clients ignore
  the extra field.

## REST and editor

- `GET /api/system/windows` lists processes that own a visible window (name and title), for the "pick from a running application" list.
- `PUT /api/devices/{id}/follow-window` turns following on or off, also for a live session. `PUT /api/preferences` carries `DefaultProfileId`.
- The editor's profile panel has an "automatic activation" section for `AppMatches` (a picker of running applications and manual exe entry).
  Preferences has a "Profiles" category with the default profile. The Pairing window has a per-device "follow the active window" switch.

## Phone app

`src/ws/connection.ts` sends `profile.lock` (`setProfileLock`) and reads `autoSwitch` from `profiles.list`. The profile drawer shows a lock switch
when the device follows the window; the profile list and manual selection always work. The browser deck (`webclient/`) does not take part.

## Not covered

Full-screen or game detection, regular expressions on the title, per-page (instead of per-profile) switching, and a `system.activeApp` variable.

## Tests

`AutoSwitchStateTests` covers the stack with a fake window source: an unknown window changes nothing; player in front, then closed; manual pick,
rule, close returns to the manual pick; the default-profile chain when the stack is empty; locked events are ignored while manual switching works;
unlocking re-evaluates; a device that did not opt in is untouched. Profile store and validator tests cover `AppMatches`.
