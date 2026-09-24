# Files and ports

## Network

| What | Where |
|---|---|
| Server | TCP port **9820**, all network interfaces |
| Devices (WebSocket) | `ws://<PC address>:9820/ws` |
| Browser deck | `http://<PC address>:9820/deck/` |
| Editor | Served at `/editor/`, but the editor API (`/api`) answers only requests from the PC itself |
| QR code content | `macrogrid://pair?host=<ip>&port=9820&pin=<pin>` |

## Data folder

Everything is in `%AppData%\MacroGrid\` (open it from the tray menu).

| Path | Content |
|---|---|
| `profiles\` | One JSON file per profile |
| `devices.json` | Paired devices and their tokens (plain text) |
| `preferences.json` | Editor and startup preferences |
| `plugins\<id>\` | Installed plugins and their settings |
| `plugin-permissions.json` | Permissions you approved for JavaScript plugins |
| `logs\` | Server logs |

## Profile files

Export writes a single `.msprofile` file and import accepts `.msprofile` or `.json`.

## Widget types in files

`button`, `toggle`, `slider`, `knob`, `label`, `image`, `web`, `plugin-html`.
