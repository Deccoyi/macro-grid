# Files and ports

## Network

| What | Where |
|---|---|
| Port | TCP **9820** on your PC. The installer opens it for private networks only. |
| Browser deck | `http://<PC address>:9820/deck/` |

Your PC's address is shown in the tray icon menu and in the Pairing window.

## Your data

Everything is in `%AppData%\MacroGrid\`. Open it from the tray icon menu.

| Path | Content |
|---|---|
| `profiles\` | One file per profile |
| `devices.json` | Paired devices and their tokens (plain text) |
| `preferences.json` | Your preferences |
| `plugins\<id>\` | Installed plugins and their settings |
| `plugin-permissions.json` | Permissions you approved for JavaScript plugins |
| `logs\` | Logs. Attach them when you report a problem. |

Exported profiles are single `.msprofile` files. Import also accepts `.json`.
