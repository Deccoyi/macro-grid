# Backup and restore

One file holds everything a person has set up, and a restore never deletes. The same file is used for the automatic restore points.

## What a backup holds

A `.mgbackup` file is a zip. It is built from the stores (profiles, the profile tree, preferences, variable definitions, per-device choices, plugin settings, language packs), not by copying files, so a file in the data folder that is half written never ends up in it.

| Entry | Content |
| --- | --- |
| `manifest.json` | `formatVersion` (1), `kind` (`backup` or `restorePoint`), `reason`, `createdAt`, `serverVersion`, `contentHash`, the plugins the profiles need |
| `profiles/<id>.json` | one profile each (the id is 1 to 64 letters, digits, `-`, `_` and must equal the entry name) |
| `profile-tree.json`, `preferences.json`, `user-variables.json` | as stored |
| `automation.json` | the automation rules, only when there are any. A restore merges them by id and saves every restored rule switched off; see [automation-rules.md](automation-rules.md) |
| `devices.json` | per paired device: id, name, assigned profile, follow-active-window, auto-switch lock. **No token.** |
| `plugin-settings/<plugin id>.json` | only from a running plugin's settings page; password fields are removed, also inside list rows |
| `languages/<tag>.json` | installed language packs |

Not in a backup: passwords, device tokens and pairings, plugin files, and the values of user variables (only their definitions).

Reading is strict: at most 2000 entries, 64 MB for one entry, 256 MB in all; unknown entry names are ignored; a newer `formatVersion` is refused with a message; a profile goes through the same checks as an import.

## Restore points

A restore point is the same file with `kind: restorePoint` in `restore-points/` of the data folder. It is made:

- before a restore,
- before a profile is deleted (inside `DELETE /api/profiles/{id}`),
- before an import overwrites a profile (the editor calls `POST /api/restore-points` with reason `import`),
- once after the server starts with a different version than the last run.

If the point cannot be written, the action does not happen. A point with the same content hash as the newest one is not written again. The newest 10 are kept, the folder is capped at 500 MB, and never fewer than 2 are kept.

## Restore

1. `POST /api/backup/inspect` opens a native Open dialog, reads the file and keeps the parsed content in one in-memory slot (30 minutes, a second inspect replaces it). The answer lists items, each *new*, *different* or *same*, and warnings (a code plus values, worded by the editor).
2. `POST /api/backup/restore` takes the slot id and the ticked items. It makes a restore point first, then applies the items through each store's own method in this order: language packs, variables, profiles (saved and broadcast), profile tree, preferences, devices, plugin settings. An item that fails is reported for that item and does not stop the others.

Rules while applying:

- Nothing is deleted. Variables are merged: a definition is added or replaced when the type matches, and skipped with a warning when it differs.
- A restore never switches "allow unencrypted connections" on.
- A device's assigned profile that does not exist here becomes none, with a warning. A device that is not paired here is left out.
- Plugin settings go through the running plugin's own settings page; a stored password is kept when the backup holds none. A plugin that refuses the values makes that item an error.

Warnings: a missing plugin, an unknown action type, a file named by an action that does not exist on this PC, a language that is not installed, and a fixed line that passwords and pairings are not part of a backup.

## Editor

File menu: "Back Up Everything…" and "Restore…". Both are in-page dialogs of the main window. Restore asks to discard unsaved edits first, shows the items with a tick each, and reloads the window afterwards. Importing a shared profile also warns about files that do not exist on this PC.
