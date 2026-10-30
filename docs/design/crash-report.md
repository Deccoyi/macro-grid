# Crash report dialog

> Status: **built** (unreleased). Nothing is sent automatically. The owner settled both open questions: no link to an issue tracker (the window says
> only "share it with the developers"), and the UI-thread behavior change is accepted. Differences from the first plan: the plugin list is wired
> after the plugins are loaded (not right after `ServerApp.Build`, where it would be empty), and two redaction rows follow what `Redactor` gives
> (`Authorization: <redacted> <redacted>`, `GET /api?token=<redacted>`). **Repositories:** `macro-grid` only. No change to the plugin SDK, the
> WebSocket protocol or the security model.

After the server crashes, the next start shows a native window (WinForms, not the editor and not a browser tab). It shows the exact report text and
offers **Save to a file**, **Copy** and **Close**. The app never sends anything, holds no credentials and opens no network connection for this.

## Decisions

| Question | Decision | Why |
|---|---|---|
| How is the report sent? | Not at all. The person saves or copies the text and shares it themselves. | No credentials in the app, no server to run, nothing leaves the PC without the person. |
| Is a file the person reads and attaches enough? | Yes, for the first version. | It needs nothing from the owner. A relay can come later if reports are wanted at volume. |
| Copy button? | Yes. | Issue forms take pasted text. The clipboard is local. The menu text "Copy" already exists (`menu.copy`). |
| What is saved or copied? | Exactly the text in the window, as plain UTF-8 text (`.txt`). No zip, no extra files. | "What you see is what you share." The full log zip stays in Help > Export logs; the window says so. |
| Language of the report text | English, always. Only the window around it follows the language preference. | The report is read by the maintainers. The same crash gives the same text on every PC. |
| Silent or autostart start | When the editor does not open at start, the window waits: a balloon says a report is ready, and the window opens when the person opens the tray menu or clicks the balloon. | Nothing pops up on an unattended sign-in. |
| A crash while starting | Shown before the server starts, even on autostart. | If the start crashes every time, a window that waits for the tray never appears. |
| Which exceptions count | Only the ones that end the process: `AppDomain.CurrentDomain.UnhandledException`, and UI-thread exceptions routed there. | `TaskScheduler.UnobservedTaskException` is not hooked: since .NET Framework 4.5 an unobserved task exception no longer ends the process (the event only reports it), so a marker for it would report a crash that never happened. |
| UI-thread exceptions | `Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)` as the first line of `Main`. | One path for every crash. **Behavior change:** today an exception on the UI thread shows the default WinForms "continue or quit" box, and the person can continue in a broken state. After this change the app ends and the report shows on the next start. |
| `MacroGrid.Windows` | Gets nothing. | Everything Windows-only here is WinForms (the form, the clipboard, the Save dialog, the UI-thread mode), and only `MacroGrid.Host` uses WinForms. The handler hookup uses `AppDomain`, which is plain .NET. An interface with one caller would only add code. |

## What the report contains

Plain text, built in memory, redacted line by line. Example (values invented):

```text
Macro Grid crash report
Nothing has been sent. Read this text before you share it.
The full log files: Help > Export logs in the editor.

Crashed at:   2026-10-29 14:03:12 +03:00
While:        running
Version:      0.5.0
Windows:      Microsoft Windows 10.0.26200
Runtime:      .NET 10.0.1
Running for:  01:02:03
Plugins:      com.example.timer 1.2.0 (Loaded), com.example.obs 0.3.1 (Error)

Error:
System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
   at MacroGrid.Core.Sessions.ClientHub.Broadcast(...)
   ...

Earlier crashes in this report (2):
  2026-10-28 09:00:01 +03:00  System.NullReferenceException (starting)
  2026-10-27 18:44:10 +03:00  (the details of this crash could not be read)

Log before the crash (server-2026-10-29.log, newest 300 lines):
14:01:55.120 [INFO] ClientHub: <device 1> connected from <ip>
...
```

- **Several markers:** the newest marker is shown in full (error and log tail). Every older one gets one line with its time, exception type and phase.
- **Plugins:** the ids, versions and load status at the moment of the crash. A crash before the plugins were loaded says `Plugins: not known yet (the crash happened while starting)`.
- **Log tail:** only from the log file named in the marker, only the bytes that existed when the app crashed (`logLength`). Lines the next start
  wrote into the same day's file are never included. Lines still in the logger's queue at the crash are lost; the error block in the marker covers that.
- **Never in the report:** profiles, preferences, plugin settings or secrets, the pairing PIN, device tokens, any other file from the data folder.

## Size caps

| What | Cap | Where it is enforced |
|---|---|---|
| Exception text in one marker (`ex.ToString()`) | 32,768 characters, cut with `...` | When the marker is written |
| One marker file | 128 KiB; a larger file is treated as unreadable | When markers are read |
| Markers read into one report | 10 newest; older ones are deleted unread | When markers are read |
| Log tail | 300 lines and 64 KiB, whichever is smaller | `CrashReport.Build` |
| One log line | `Redactor.MaxLineLength` (8000), as today | `Redactor.Line` |
| Whole report text | 160 KiB; the log tail is cut first | `CrashReport.Build` |

## Marker file

- Folder: `<data>\crashes\` (`<data>` is `ProfileStore.DefaultDataDir`, so `MACROGRID_DATA_DIR` is honoured in a development build).
- Name: `crash-yyyyMMdd-HHmmss-fff.json` in local time. A name that already exists gets `-2`, `-3` and so on (`FileMode.CreateNew`).
- Content: UTF-8 JSON, written once and never changed. Raw, not redacted: it is a local file in the person's own data folder, like the logs.
  Redaction happens when the report is built, because device names are needed for it.

```json
{
  "format": 1,
  "crashedAt": "2026-10-29T14:03:12.345+03:00",
  "phase": "running",
  "isTerminating": true,
  "macroGrid": "0.5.0",
  "os": "Microsoft Windows 10.0.26200",
  "runtime": ".NET 10.0.1",
  "uptimeSeconds": 3723,
  "exceptionType": "System.InvalidOperationException",
  "details": "System.InvalidOperationException: Collection was modified; ...\r\n   at ...",
  "plugins": [
    { "id": "com.example.timer", "version": "1.2.0", "status": "Loaded" }
  ],
  "logFile": "server-2026-10-29.log",
  "logLength": 482113
}
```

- `phase`: `starting` until the tray icon exists, then `running`.
- `plugins`: `null` when the crash came before the plugins were loaded.
- `logFile` and `logLength`: today's log file name (`LogRetention.FileName`) and its length at the crash; `null` and `0` when it does not exist.
- `format`: a reader that sees a higher number treats the marker as unreadable.

## Crash handler rules

The handler runs while the process is dying. It must be small and must never make things worse.

- Install it first thing in `Main`, before the single-instance check, the agreement gate and the server.
- It does not touch dependency injection, `HostText`, preferences, the logger or the UI. It writes one file synchronously and returns.
- **Crash in the crash:** a static flag (`Interlocked.Exchange`) lets only the first crash write. A second crash on another thread returns at once. Single crash wins: only the first crash of a process is recorded, and a second one that follows it is lost.
- **No blocking in the handler:** the plugin list is read with `PluginManager.TryGetPlugins` (`Lock.TryEnter`, no waiting). If another thread holds the lock, the marker says "not known" like a crash before the plugins were loaded.
  The whole handler body is inside `try { } catch { }`. `ex.ToString()` gets its own `try`; when it throws, `details` holds the type name only.
  The plugin snapshot gets its own `try`; when it throws, `plugins` is `null`.
- **Marker write fails** (folder cannot be created, disk full, no access): the handler gives up silently. The next start shows nothing. The log
  still has whatever reached the disk. This is accepted and listed under risks.
- It does not call `Environment.Exit`, and it shows no window. The runtime ends the process as it does today.
- **Not caught at all:** stack overflow, `Environment.FailFast`, a kill from Task Manager, power loss, and some out-of-memory cases. No handler runs
  there, so no report appears. The plan does not try to fix that (no watchdog process).

## When the window shows

`CrashReportPolicy.When(CrashMarkerSet set, bool editorOpensAtStart)` in Core decides. An unreadable marker counts as `running` (its phase is
unknown). The values:

| Markers found | Start | Result | What happens |
|---|---|---|---|
| none | any | `None` | Nothing. |
| at least one with `phase: starting` | any | `BeforeServer` | Right after the agreement gate, before `ServerApp.Build`: delete the markers, then show the window modal. The server starts after it closes. |
| only `running` | the editor opens | `WithEditor` | After the editor window opens: delete the markers, then show the window (not modal, on top). |
| only `running` | tray only (autostart or launch mode "tray") | `OnTrayMenu` | A balloon replaces the usual "running" or "updated" balloon. The first time the tray menu opens (that opening is cancelled) or the balloon is clicked: delete the markers, then show the window. If the person never opens it, the markers stay for the next start. |

- The report text is always built **before** anything is deleted, and the markers are deleted **before** the window shows. A crash inside the window
  or in the next run can then never show the same report forever.
- Deleting a marker that cannot be deleted is ignored. It shows again on the next start; that is accepted.
- An unreadable marker (half written, bad JSON, too large, a newer `format`) counts as "a crash happened, the details could not be read". It is listed
  as such and deleted with the others.
- If building the report throws (a bug), the app logs a warning, deletes the markers and starts normally. A broken report must never stop the app.
- Report building needs no running server: the plugins come from the marker, and the device names for redaction come from a read-only reader of
  `devices.json` (names are plain JSON there; only tokens are protected). So one code path serves all three cases.

## Redaction

Reuse `Redactor` and `RedactionContext.ForThisPc(dataDir, deviceNames)` unchanged. Every line of the report goes through `Redactor.Line`
(the header lines too, since they are cheap). Then a crash-only path pass runs (`CrashReportPaths.Clean`), because the plan promises that no file path
outside the app's own folders is shown, and `Redactor` today leaves such paths alone on purpose (a test pins `C:\Program Files\...` as unchanged).
`Redactor` itself is **not** changed, so the Error List export and the log export stay exactly as they are.

Crash-only path rule (after `Redactor`, so `<home>` and `<data>` are already in place):

- A path under the install folder (`AppContext.BaseDirectory`) becomes `<app>\` plus the rest. Those are the app's own files.
- A path under `<home>` or `<data>` becomes `<path>` as well (the rest of it can hold a document name or a profile name). Only `<data>\logs\` stays.
- Any other absolute path (`X:\...` or `\\server\share\...`) becomes `<path>`. Folder names may hold spaces; the last part ends at the first
  whitespace, quote, `<`, `>`, `|` or `:` (so `:line 42` in a stack trace stays). Best effort: a file name with spaces can leave its tail behind.
- A label already in place (`<app>`, `<data>\logs\`) is never touched again.
- The host of any `scheme://host[:port]` URL becomes `<host>` and the URL loses its path and query (loopback hosts stay). A dotted name with a port (`nas.home.lan:443`) becomes `<host>`. A MAC address (`:` or `-` form) becomes `<mac>`. There is no general host-name rule.
- The report builds its `Redactor` with `minNameLength: 2`, so a two-letter user name or PC name is removed too (the other exports keep 3).
- Linear-time regex (`RegexOptions.NonBacktracking`), like the rest of `Redactor`.

Test cases (redaction context: home `C:\Users\Jane Doe`, data `C:\Users\Jane Doe\AppData\Roaming\MacroGrid`, user `janedoe`, PC `OFFICE-PC`,
devices `Jane's Phone`, `Tab (2)`; install folder `C:\Program Files\Macro Grid\`):

| Input | Expected |
|---|---|
| `   at Plugin.Run() in C:\Users\Jane Doe\source\p\Run.cs:line 42` | `   at Plugin.Run() in <path>:line 42` |
| `Loaded C:\Users\Jane Doe\AppData\Roaming\MacroGrid\plugins\a\a.dll` | `Loaded <path>` |
| `Loaded C:\Program Files\Macro Grid\MacroGrid.Core.dll` | `Loaded <app>\MacroGrid.Core.dll` |
| `   at X.Y() in D:\build\agent\src\X.cs:line 7` | `   at X.Y() in <path>:line 7` |
| `Launch D:\Private\taxes.xlsx failed` | `Launch <path> failed` |
| `Open \\nas\share\notes.txt` | `Open <path>` |
| `InvalidOperationException: device Jane's Phone was removed` | `InvalidOperationException: device <device 1> was removed` |
| `user janedoe on OFFICE-PC` | `user <user> on <pc>` |
| `connect to 192.168.1.20:8765 failed` | `connect to <ip>:8765 failed` |
| `listening on 127.0.0.1` | unchanged |
| `GET /api?token=abc123&x=1` | `GET /api?token=<redacted>&x=<redacted>` |
| `Authorization: Bearer eyJhbGciOi` | `Authorization: Bearer <redacted>` |
| `pairing pin: 482913` | `pairing pin: <redacted>` |
| `mail jane@example.com` | `mail <email>` |
| `see https://example.com/docs` | unchanged |

The build agent checks each expected value against `Redactor` as it is; where `Redactor` already gives a slightly different but safe result, the
test follows `Redactor` and the plan is not a reason to change it.

## Files and classes

### `MacroGrid.Core` (all logic, all tested)

| File | Contents |
|---|---|
| `src/MacroGrid.Core/Diagnostics/CrashMarker.cs` | `public sealed record CrashMarker(...)` with the fields of the JSON above; `public sealed record CrashPlugin(string Id, string Version, string Status)`; `public enum CrashPhase { Starting, Running }`; `public static CrashMarker From(Exception ex, CrashPhase phase, DateTimeOffset now, string version, TimeSpan uptime, IReadOnlyList<CrashPlugin>? plugins, string logsDir)`. |
| `src/MacroGrid.Core/Diagnostics/CrashMarkers.cs` | `public static class CrashMarkers`: `FolderName = "crashes"`, the caps as constants, `bool TryWrite(string crashesDir, CrashMarker marker)` (never throws), `CrashMarkerSet Read(string crashesDir)` (newest first, unreadable ones counted, all file paths kept for deletion), `void Delete(IEnumerable<string> paths)` (never throws). `public sealed record CrashMarkerSet(IReadOnlyList<CrashMarker> Markers, IReadOnlyList<DateTimeOffset?> Unreadable, IReadOnlyList<string> Files)`. |
| `src/MacroGrid.Core/Diagnostics/CrashReport.cs` | `public static class CrashReport`: `string Build(CrashMarkerSet set, string logsDir, Redactor redactor, string installDir)`. Reads the log tail (shared read, like `LogExport`), applies `Redactor.Line` and `CrashReportPaths.Clean` to every line, enforces the caps. |
| `src/MacroGrid.Core/Diagnostics/CrashReportPaths.cs` | `public static partial class CrashReportPaths`: `string Clean(string line, string installDir)`. |
| `src/MacroGrid.Core/Diagnostics/CrashReportPolicy.cs` | `public enum CrashReportTiming { None, BeforeServer, WithEditor, OnTrayMenu }`; `public static CrashReportTiming When(CrashMarkerSet set, bool editorOpensAtStart)`. |
| `src/MacroGrid.Core/Devices/DeviceStore.cs` | Add `public static IReadOnlyList<string> ReadNames(string dataDir)`: reads `devices.json` read-only with the same JSON options, returns the names, never writes, never needs the protector, returns empty on any error. |

### `MacroGrid.Host` (thin, not unit tested)

| File | Contents |
|---|---|
| `src/MacroGrid.Host/CrashCapture.cs` | `internal static class CrashCapture`: `Install(string dataDir)` (sets the UI-thread mode, subscribes `AppDomain.CurrentDomain.UnhandledException`), `MarkRunning()` (phase becomes `Running`), `Plugins` (a `Func<IReadOnlyList<CrashPlugin>>?` set after the plugins are loaded). The handler body follows "Crash handler rules". DEBUG only: the test switch below. |
| `src/MacroGrid.Host/Ui/CrashReportDialog.cs` | `internal sealed class CrashReportDialog : Form`, styled like `AgreementDialog`: intro label, read-only monospace multiline `TextBox` with the report, buttons Save to a file, Copy, Close; a status label for "Saved" or "Copied". Save uses `SaveFileDialog` with the suggested name `macro-grid-crash-yyyyMMdd-HHmm.txt` and writes the exact `TextBox` text as UTF-8 without BOM. Copy uses `Clipboard.SetText` in a `try` (the clipboard can be locked by another app; then the status says it failed). |
| `src/MacroGrid.Host/Program.cs` | Calls `CrashCapture.Install` first; after the agreement gate reads the markers, builds the report, asks `CrashReportPolicy`; handles `BeforeServer` there; sets `CrashCapture.Plugins` after `ServerApp.Build`; passes a pending report (text and files) and its timing to `TrayContext`. |
| `src/MacroGrid.Host/Ui/TrayContext.cs` | Takes the pending report. Calls `CrashCapture.MarkRunning()` at the end of its constructor. `WithEditor`: shows the window after `OpenEditor()`. `OnTrayMenu`: shows the crash balloon instead of the usual one, hooks `ContextMenuStrip.Opening` and the balloon click once. |
| `src/MacroGrid.Host/HostText.cs` | The new keys below. |

## Texts (`HostText.cs`)

Native shell texts live in `HostText.cs` as `(En, Tr)` pairs, not in the editor's `en.ts` / `tr.ts`. The report text itself never goes through
`HostText.Get` (it would run `string.Format` on it); it is set on the `TextBox` as is.

| Key | English | Turkish |
|---|---|---|
| `crash.title` | Macro Grid crash report | Macro Grid çökme raporu |
| `crash.intro` | Macro Grid closed unexpectedly. This is the report. Nothing has been sent. You can save it or copy it and share it with the developers if you want. | Macro Grid beklenmedik şekilde kapandı. Rapor aşağıda. Hiçbir şey gönderilmedi. İsterseniz kaydedip ya da kopyalayıp geliştiricilerle paylaşabilirsiniz. |
| `crash.save` | Save to a file... | Dosyaya kaydet... |
| `crash.copy` | Copy | Kopyala |
| `crash.close` | Close | Kapat |
| `crash.saved` | Saved to {0} | {0} konumuna kaydedildi |
| `crash.saveFailed` | Could not save the file: {0} | Dosya kaydedilemedi: {0} |
| `crash.copied` | Copied. | Kopyalandı. |
| `crash.copyFailed` | Could not copy. Try again. | Kopyalanamadı. Tekrar deneyin. |
| `crash.fileFilter` | Text file (*.txt)\|*.txt | Metin dosyası (*.txt)\|*.txt |
| `tray.crashReady` | Macro Grid closed unexpectedly last time | Macro Grid son seferde beklenmedik şekilde kapandı |
| `tray.crashReadyHint` | Click here or open this menu to see the report. | Raporu görmek için buraya tıklayın veya bu menüyü açın. |

## Build steps (one commit each, Conventional Commits, on a branch from `dev`)

1. `feat(diagnostics): crash marker file format` - `CrashMarker`, `CrashMarkers` (write, read, delete, caps) and their tests.
2. `feat(diagnostics): crash-only path cleaning` - `CrashReportPaths` and the path rows of the redaction table as tests.
3. `feat(diagnostics): read device names without the token protector` - `DeviceStore.ReadNames` and its test.
4. `feat(diagnostics): build the crash report text` - `CrashReport.Build` and its tests (including the whole redaction table end to end).
5. `feat(diagnostics): decide when the crash report shows` - `CrashReportPolicy` and its tests.
6. `feat(host): write a crash marker when the app crashes` - `CrashCapture`, the UI-thread mode, the DEBUG test switch, `Program.cs` hookup.
   After this commit a crash leaves a marker, nothing shows it yet.
7. `feat(host): crash report window` - `CrashReportDialog`, the `HostText` keys, the three timings in `Program.cs` and `TrayContext`.
8. `docs: crash report` - `docs/CHANGELOG.md` under `[Unreleased]` / New, one line, for example "After a crash, Macro Grid shows a report on the
   next start that you can save or copy. Nothing is sent." Fix the crash report line in `docs/roadmap.md` (it still says the app sends the
   report itself). Move this plan to `docs/design/crash-report.md` as the reference and delete its row in `docs/plans/README.md`.
   `docs/CHANGELOG-developer.md` gets nothing (no SDK, protocol or migration change).

## Tests (`tests/MacroGrid.Tests/`, xUnit, temp folder per test class like `LogExportTests`)

`CrashMarkerTests`
- `From` fills every field; `details` is cut at 32,768 characters; an exception whose `ToString()` throws gives the type name only.
- `logFile` and `logLength` come from today's log file; both empty when there is none.
- Round trip: `TryWrite` then `Read` gives an equal marker.
- Two writes in the same millisecond give two files (`-2` suffix).
- `TryWrite` into a path that cannot be created returns false and does not throw.
- `Read` returns newest first; reads at most 10; older files are in `Files` so they get deleted.
- Unreadable: bad JSON, an empty file, a file over 128 KiB, `format: 2`. Each counts in `Unreadable` and is in `Files`.
- Files not named `crash-*.json` in the folder are ignored and never deleted.
- `Delete` ignores a locked or missing file.

`CrashReportPathsTests`
- The path rows of the table. A path under `<home>` or `<data>` becomes `<path>` except the log folder; URL hosts, `host:port` and MAC addresses are removed. A line of 8000 characters runs in well under a second.

`CrashReportTests`
- The whole redaction table, end to end through `Build`.
- The log tail stops at `logLength`: lines appended after it (the next start) are not in the report.
- The log tail keeps the newest 300 lines and at most 64 KiB; the whole text stays under 160 KiB with a huge exception and a huge log.
- A missing log file: the report says so and has no log section.
- Several markers: the newest in full, the others one line each, unreadable ones as "could not be read".
- `plugins: null` gives "not known yet".
- Nothing from `profiles/`, `devices.json` or `preferences.json` appears, even when the log folder sits next to them.

`CrashReportPolicyTests`
- No markers gives `None`; any `starting` marker gives `BeforeServer` with and without the editor; only `running` gives `WithEditor` or `OnTrayMenu`.

`DeviceStoreTests` (add)
- `ReadNames` returns the names from a `devices.json` with protected tokens without a protector, returns empty for a missing or broken file, and
  does not change the file (same bytes and write time after the call).

## Manual tests for the owner

A development build has a crash switch, read once at start, only in DEBUG (same pattern as `MACROGRID_DATA_DIR` and `MACROGRID_PORT`):
`MACROGRID_TEST_CRASH=starting` throws right after `CrashCapture.Install`; `MACROGRID_TEST_CRASH=running` throws on a background thread 5 seconds
after the tray icon appears; `MACROGRID_TEST_CRASH=ui` throws on the UI thread 5 seconds after the tray icon appears. The exception message
contains the user name, the PC name and `D:\Private\test.txt`, so redaction is visible. Use a separate data folder so the real one is not touched.

1. Development build, `MACROGRID_DATA_DIR` set to an empty test folder, `MACROGRID_TEST_CRASH=running`: the app ends after 5 seconds. Start it
   again without the switch. The editor opens and the report window shows. Check: no user name, no PC name, `<path>` instead of the test path.
2. Same with `ui`. Expected: no WinForms "continue or quit" box; the app ends; the report shows on the next start with the UI-thread error.
3. Same with `starting`. Start again without the switch: the report window shows before the tray icon appears.
4. Crash with `running`, then start with `--autostart` (or launch mode "tray"). Expected: no window, a balloon. Open the tray menu: the window
   opens instead of the menu. Close it, open the menu again: the normal menu.
5. Crash, then start with `--autostart` and exit from the tray without opening the menu. Start again: the report is still there.
6. Save to a file: open the file in a text editor; it is exactly the text in the window. Copy: paste into a text editor; same text.
7. Crash three times in a row without looking. Next start: one report, the newest in full, two earlier lines.
8. Release build, no crash needed: write the example marker from this plan into `%AppData%\MacroGrid\crashes\crash-20261029-140312-345.json`
   and start the installed app, once from the Start menu and once with `--autostart`. Then write a file with `{` only into the same folder and start
   again: the window says a crash happened and the details could not be read.
9. End the app from Task Manager. Next start: no report (expected: no handler runs on a kill).

## Risks

- **Behavior change on the UI thread.** An exception that the person could "continue" past today now ends the app. That is honest (the state was
  broken), but a UI bug that was harmless before now closes the tray. Watch the first reports after release.
- **No marker when the write fails** or the process dies without a handler (stack overflow, kill, power loss). The log on disk is the only trace.
- **Lost log lines.** The file logger writes from a queue; lines queued at the crash never reach the disk. The marker's error block covers the
  cause, not the last few lines before it.
- **Redaction is best effort.** Names the redactor does not know (a profile name, a window title, a file name with spaces) can stay. The window
  says to read the text before sharing; the person sees all of it before anything leaves the PC.
- **A start that crashes every time.** The report shows before the server, so the person sees it, but the app still cannot start. The report is the
  way to tell the developers; nothing here repairs it.
- **Marker that cannot be deleted** shows again on every start. Rare (permissions in the person's own data folder).
- **Ghost tray icon** after a crash stays until the mouse passes over it. Not part of this plan.

## A build agent must not

- Send anything over the network, add any mail, upload or relay code, or put any address or credential into the code.
- Open a browser, a mail program or a web page from the window.
- Change `Redactor`, `LogExport` or `ProblemExport` behavior (the crash-only path pass is a separate class).
- Put a marker into the report raw, or write a redacted text anywhere but the place the person picks in the Save dialog.
- Show a window, log through dependency injection or touch preferences inside the crash handler.
- Write a marker for `TaskScheduler.UnobservedTaskException`.
- Ship the test crash switch outside `#if DEBUG`.
- Put the window texts in the editor's `en.ts` / `tr.ts`, or hard-code them in the form.
- Bump the version, tag, push, merge or release anything.
- Name third-party products in code, comments, docs or commits.

## Open questions for the owner (answered)

1. Answered: no link, no place named. Original question: should the window say **where** to share the report (for example "open an issue on the project's issue tracker"), and should it link there?
   A link would put a hosting site's address into the code, and a link opens a browser. The build leaves it out until the owner says yes; the intro
   text then says only "share it with the developers".
2. Answered: accepted. Original question: the UI-thread behavior change (see Decisions and Risks): accept it as planned? The alternative is to keep the old "continue or quit" box for
   UI-thread errors and report only crashes from other threads.
