# Preferences

Open **Settings → Preferences…** in the editor. While a settings window is open, the editor behind it is blocked.

| Category | Options |
|---|---|
| **General** | Start Macro Grid when I sign in to Windows, and what that start does (tray only, or open the editor). What happens when you open Macro Grid manually (editor window or tray only). Whether Macro Grid checks for updates by itself, and whether pre-releases (beta versions) count. |
| **Appearance** | **Theme**: dark or light. |
| **Language** | Editor language: Turkish (default) or English, and the language packs you add. |
| **Profiles** | The **default profile** used when a device has no assignment and no auto-switch rule applies. |
| **Preview profiles** | Add or remove device sizes (name, width, height) for the editor's preview. |
| **Global Variable List** | Your own text, number and True / False variables. See [Variables and text](/guide/variables#your-own-variables). |
| **Automation** | Rules that run a list of actions by themselves, and the switch that pauses them all. See [Automation rules](/guide/automation). |

## Language packs

The editor ships in Turkish and English. To add another language, open **Language** and export a table as CSV. Translate the texts in the **translation** column, then import the CSV back. The Language page shows how much is translated and how many rows need review, and offers **Use**, **Export CSV**, **Import CSV** and **Remove**. A language pack changes only the texts of the editor, never how things work. Permission and removal windows also show the English text in brackets.

## Notices

- **Allow unencrypted connections** is on by default. Turn it off and only the encrypted port serves other devices. See [Security](/guide/security).
- Hints under a web widget's address can be closed for now or for good. **Show closed notices again** brings back the ones closed with "Do not show again".

Preferences are saved in `%AppData%\MacroGrid\preferences.json`.
