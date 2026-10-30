# Troubleshooting

## The phone cannot connect

- PC and phone must be on the **same network**. Guest Wi-Fi and "client isolation" often block device-to-device traffic.
- Check the address in the tray menu or the Pairing window and enter it exactly, with port `9820`.
- The firewall must allow TCP **9820** and **9821** on your network profile. The installer adds a rule for private and domain networks. If Windows classed your network as **Public**, change it to Private.
- Is the server running? Look for the tray icon.
- Try `http://<PC address>:9820/deck/` in the phone's browser to separate app problems from network problems.

## The PIN is rejected

The PIN works only while the **Pairing** window is open, for at most five minutes, and for one device. Keep the window open while you enter it, or click **Generate a new code**. After too many wrong PINs the device has to wait a little (30 seconds at first) before it can try again.

## Pairing window shows no QR code

No local network address was found. Connect the PC to Wi-Fi or Ethernet.

## A button does nothing

Look at the phone for an error message, and at **Diagnostic Messages** in the editor. The usual causes are a stale binding (a deleted OBS scene), a plugin that is turned off or not connected, or a program path that no longer exists. Remember that **Long press** and **Double tap** fire in addition to Press.

## My changes do not show on the phone

Click **Save**. Nothing is sent before that.

## A live value is empty

The variable does not exist (typo, or its plugin is not installed or connected). Use **+ Add variable** to pick from the list, and **Refresh variables** after installing a plugin.

## A dynamic rule never matches

For a boolean variable type `true` or `false`, not `1`, `0` or *Açık*. Text comparisons ignore case but must match exactly.

## OBS shows "wrong password" or "OBS is not running"

Enable the WebSocket server in OBS (**Tools → WebSocket Server Settings**), and enter the same password in the OBS plugin settings. A wrong password stops retries until you change the settings.

## Windows SmartScreen warns about the installer

The installer is not code-signed yet. Choose **More info**, then **Run anyway**.

## Diagnostic Messages

**Diagnostic Messages** (the bottom panel, formerly the Error List) lists what is wrong with your profile or plugins, each line with a code. Double-click a line, or press its button, to see the full message, the code, how often it happened and where it is. **Go to widget** opens the faulty widget. Nothing is uploaded: copy or save the list with the buttons in the panel, and share it yourself.

- Double-click a column border to fit the column to its content. Right-click the table header to choose which columns are shown.
- A button with only warnings gets a small yellow triangle in the editor; a button with an error keeps the red mark. Hover to see all its lines.

| Code | What it means | What to do |
|---|---|---|
| `E210` | A button runs an action whose plugin is not available. | Install or switch on the plugin, or choose another action. |
| `E211` | A plugin widget is not available. | Install or switch on the plugin, or delete the widget. |
| `E212` | A rule compares a number variable with text, so it can never be true. | Type a number in the rule. The profile can still be saved. |
| `E220` | An action points to a page, profile or web widget that no longer exists. | Choose a target that exists. |
| `W221` | A setting of an action is not an allowed value. | Choose a valid value. |
| `W230` | A text or rule uses a variable that no plugin provides right now. | Check that the plugin is on and the name is spelled right. |
| `W231` | A text, rule or action uses a name that is not in the Global Variable List. | Add it again in Preferences or change the name. |
| `W233` | The If, Else and End steps of an action list do not match. | Fix the blocks. |
| `W234` | An If step has no condition. | Pick a variable for it. |
| `W235` | A widget uses a variable that has no value right now. | Check the plugin. A placeholder or an "is unavailable" rule avoids the warning. |
| `P114` | An official plugin's files changed after it was installed, so it does not run. | Reinstall it from the Plugins window. |
| `P122` | A plugin failed and was switched off. | Update it, then switch it on in the Plugins window. |
| `P131` | A button was pressed, but one of its actions is not available. | As for `E210`. |
| `P132` | An action of a button failed when it ran. | Check the action's settings. The line goes away after a run without a failure. |
| `P160` | A step of an automation rule did not work. | Check that step under Automation. |
| `P161` | An automation rule started too often, so some starts were dropped. | Check what triggers it. |
| `P162` | An automation rule was switched off because it kept starting. | Fix its trigger, then switch it on again. |
| `P163` | An automation rule uses a global variable that does not exist. | Pick another variable in the rule, or add the variable again. |

## A button is grey

A button that cannot work right now looks grey with a small warning mark, on the phone and in the editor. Press it on the phone to see what is missing. See [The phone app](/guide/phone-app).

## Crash report

After a crash, the next start of Macro Grid shows a **crash report** window with the report text. **Save to a file** writes it as a text file, **Copy** copies it, and **Close** closes the window. Nothing is sent. Share the file yourself, for example with an issue. The report is always in English. If the editor does not open at start, the window waits: a notice in the tray tells you a report is ready, and the window opens when you open the tray menu or click the notice.

## Logs

Logs are in `%AppData%\MacroGrid\logs\`. Include them when you [open an issue](https://github.com/Deccoyi/macro-grid/issues).

To save all the logs as one zip file, use **Help → Export Logs…**. Personal details are removed where they can be recognized, but look through the files before you share them.
