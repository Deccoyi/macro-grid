# Diagnostics and repair

**Status:** built. **Repositories:** `macro-grid` only (`src/MacroGrid.Core/Diagnostics`, `src/MacroGrid.Host/Api`, `editor/src/diagnostics`).

What the Error List ("Diagnostic Messages") offers beyond listing lines: names for widgets so a line can say which button it is about, a
check of the open profile, a way to take the person to the faulty widget, and two exports (the Error List and the log files) that remove
personal details before anything leaves the computer. Nothing is uploaded anywhere; the person saves or copies the result and shares it.

## Redaction

`Redactor` (`MacroGrid.Core.Diagnostics`) cleans one line of text. It is best effort and says so in every export. Rules, in this order:

1. Known folders: the data folder becomes `<data>`, the user's home folder `<home>`; any `X:\Users\<name>` becomes `<home>` too.
2. The Windows user name and the computer name (whole words, at least 3 characters) become `<user>` and `<pc>`.
3. Names of paired devices become `<device 1>`, `<device 2>`, ... .
4. Web addresses lose a user name and password and the values of their query parameters.
5. `Bearer <token>`, and `name=value` / `name: value` where the name looks like a secret (password, token, key, secret, a whole-word `pin`).
6. IPv4 and IPv6 addresses become `<ip>` (loopback and `0.0.0.0` stay), e-mail addresses become `<email>`.
7. Any run of 32 or more letters, digits and `-_+/=` becomes `<redacted:N>`.

The patterns are non-backtracking, the ones built from this computer's names have a 250 ms limit, and a line is cut at 8000 characters, so
a hostile log line cannot stall an export. `tests/MacroGrid.Tests/RedactorTests.cs` is the list of what is and is not removed.

## The two exports

- **Error List** (Copy and Save in the panel): the editor sends its own lines as text, the server adds its own (`ProblemExport`), and the
  server redacts everything and answers with one JSON document. At most 500 lines and 256 KB; texts are cleaned and cut. `POST
  /api/problems/export` with `{ to: "text" | "file", lines }`: `text` returns the document (the editor copies it), `file` opens the Save dialog.
- **Log files** (Help > Export logs): `LogExport` reads only files named like a daily server log, newest first, redacts them line by line
  and stops at 10 MB of redacted text (the oldest file kept is cut at its start, with a marker line; older files are left out and listed in
  `info.json`). The zip holds `logs/`, `info.json` (versions, system, plugin names and states, counts, the Error List) and `README.txt`.
  Profiles, preferences, plugin data and pairing files are never read. `GET /api/diagnostics/logs-preview` lists what would be included;
  `POST /api/diagnostics/export-logs` builds the zip and opens the Save dialog. The log of the current day is read while it is open.

## Widget names

Every widget has a `name`: 1 to 64 characters, control characters removed, unique on its page without regard to case. It is for people
(messages, pickers, the Web action's target list); it is never used as a reference, ids are.

- New widgets get `Prefix_n` (`Button_1`, `Toggle_1`, `Label_1`, `Slider_1`, `Knob_1`, `Image_1`, `Web_1`, otherwise `Widget_1`). These
  default names are not translated.
- Copy, paste, duplicate and move keep a name when it is free on the target page, otherwise add `_2`, `_3`, ... .
- A name is edited in Properties. An empty or taken name is refused with a line under the field and the old name comes back.
- An old profile has its missing or repeated names filled in **in memory** when it is loaded (`WidgetNames.Ensure`). The file is not
  rewritten at start; the next normal save writes the names. Import and the profile API repair names the same way, and a save never
  fails because of a name.
- The rules exist twice (server `WidgetNames`, editor `state/widgetNames.ts`); `tests/shared/widget-name-cases.json` is what both are tested against.

## The profile check

`editor/src/diagnostics/profileCheck.ts` is a pure function over the open profile and what the server offers right now. Source id
`profile-check`; every run replaces the earlier lines. It runs 300 ms after the profile, the action list or the plugin widgets change
(`ProfileCheckReporter`), and not until the action list has loaded. The action list and the plugin widgets are refetched every 10 seconds
while the window is visible, so removing or switching off a plugin shows up without a click.

| Code | Severity | Finding |
|---|---|---|
| `E210` | error | An action's type is not in the action list (its plugin was removed, is switched off or failed to load). |
| `E211` | error | A plugin widget whose plugin or widget is not available. |
| `E220` | error | A "Switch page" to a page that is not in the profile, a "Switch profile" to a profile that does not exist, a "Change web page" to a web widget that does not exist. |
| `W221` | warning | A setting of a described action is not one of its fixed options or is outside `min` / `max`. Fields whose options come from the plugin at run time are not checked. |
| `W230` | warning | A variable used in a text, a rule or an action text that no plugin provides right now. |

Nothing is changed in the profile: reinstalling the plugin makes the lines disappear at the next refresh, and the buttons work again.

## Where a line points

A line may carry a target: profile, page, widget, event and the number of the action. The server's `Problem.Target` is returned by `GET
/api/problems` as `target`; the editor's own lines use `DiagnosticTarget`. Only ids are stored. The panel looks the names up in the open
profile and shows ids for a line about another profile.

- **Details:** a button in each row (or a double-click) opens the full message, its code, source, count, first and last time, where it is,
  and the help text `errorHelp.<code>`. A test keeps a text for every code in both languages.
- **Go to widget:** opens the profile (through the usual unsaved-changes question), the page, selects the widget, brings Properties
  forward and, when the line names an event, opens that event's actions and outlines the action once.
- **Red marker:** a widget with an error line gets a small red alert icon in its corner on the editor canvas (in the overlay, never inside
  the rendered widget), with the message as its tooltip. An action whose type is unknown shows the same icon in the action list.

## A press on a missing action

`ActionDispatcher.DispatchAsync` no longer skips an unknown action silently: it returns a failure with `Missing = true` and the text
"'type' is not available. Its plugin may be removed or switched off.", and the actions after it still run. `ClientHub` shows the text
on the phone (the existing `action_failed` toast, no protocol change) and reports `P131` to the Error List, one line per widget and action
type with a count and a target. The lines are removed when any plugin finishes loading; a press that is still broken puts its line back.
The editor then shows two lines for one broken button: `E210` (what is wrong in the profile) and `P131 xN` (it was pressed N times).
