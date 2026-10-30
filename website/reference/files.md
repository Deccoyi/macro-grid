# Files and ports

## Network

| What | Where |
|---|---|
| Port | TCP **9820** on your PC. The installer opens it for private networks only. |
| Encrypted port | TCP **9821** on your PC (`wss://`, `https://`). The installer opens it for private networks only. See [Security](/guide/security). |
| Browser deck | `http://<PC address>:9820/deck/` |

Your PC's address is shown in the tray icon menu and in the Pairing window.

## Your data

Everything is in `%AppData%\MacroGrid\`. Open it from the tray icon menu.

| Path | Content |
|---|---|
| `profiles\` | One file per profile |
| `devices.json` | Paired devices. Their tokens are encrypted with Windows DPAPI for your account. |
| `preferences.json` | Your preferences |
| `plugins\<id>\` | Installed plugins and their settings |
| `plugin-permissions.json` | Permissions you approved for JavaScript plugins |
| `user-variables.json`, `automation.json` | Your global variables and automation rules |
| `restore-points\` | Automatic copies of your data, made before a restore, a profile delete or an import, and after an update. See [Profiles and pages](/guide/profiles#back-up-and-restore). |
| `logs\` | Logs. Attach them when you report a problem. |

Shared profiles are single `.mgprofile` files. Import also accepts older `.msprofile` files and `.json`.
