# Plugin tool

A command-line tool for people who write JavaScript plugins for Macro Grid. It is a separate download on the release page (`MacroGrid-PluginTool-<version>-win-x64.zip`); the app does not need it.

It uses the same rules and the same runtime as Macro Grid, so a plugin it accepts is one the app accepts.

| Command | What it does |
| --- | --- |
| `validate <folder>` | Checks a plugin folder without running anything: manifest, id, version, compatibility, start file (it must be inside the folder and parse), permissions, widgets, icon, languages and size. |
| `pack <folder> [--out dir]` | Runs the check, then writes `<id>-<version>.zip` and `<zip>.sha256`. Saved plugin data (`settings.json`, `storage.json`) and hidden files are left out. It does not sign anything. |
| `new <id> [--name text] [--out dir]` | Starts a plugin in `<dir>/<id>` with a manifest, a small working script and a README. |
| `run <folder> [options]` | Starts the plugin for real in a temporary data folder and lists its permissions, actions, settings, status items and variables. |

`run` options: `--action <type>` runs an action once; `--settings <json>` gives that action its settings; `--value <n>` is the slider value the action sees; `--deny <permission>` starts the plugin without a permission (repeat it for more); `--seconds <n>` keeps it running so timers can fire; `--data <dir>` uses a folder you choose instead of a temporary one (kept afterwards).

Use `run` on your own plugin. To try someone else's plugin, install it in the app, where it asks for approval of its permissions.

Macro Grid's own ports are never reachable from a plugin, also in `run`.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Done. |
| 1 | The plugin has errors, or the command failed. |
| 2 | The command line was not understood. |

`validate` prints one `error:` or `warning:` line for each finding and ends with `OK: <id> <version>` or `FAILED: <n> error(s)`.

A package made by `pack` is byte-identical when you pack the same folder again with the same version of the tool; another version of the tool may compress differently.
