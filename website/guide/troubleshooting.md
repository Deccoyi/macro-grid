# Troubleshooting

## The phone cannot connect

- PC and phone must be on the **same network**. Guest Wi-Fi and "client isolation" often block device-to-device traffic.
- Check the address in the tray menu or the Pairing window and enter it exactly, with port `9820`.
- The firewall must allow TCP **9820** on your network profile. The installer adds a rule for private and domain networks. If Windows classed your network as **Public**, change it to Private.
- Is the server running? Look for the tray icon.
- Try `http://<PC address>:9820/deck/` in the phone's browser to separate app problems from network problems.

## The PIN is rejected

The PIN works only while the **Pairing** window is open, for at most five minutes, and for one device. Keep the window open while you enter it, or click **Generate a new code**. After too many wrong PINs the device has to wait a little (30 seconds at first) before it can try again.

## Pairing window shows no QR code

No local network address was found. Connect the PC to Wi-Fi or Ethernet.

## A button does nothing

Look at the editor's status bar and the phone for an error message. The usual causes are a stale binding (a deleted OBS scene), a plugin that is turned off or not connected, or a program path that no longer exists. Remember that **Long press** and **Double tap** fire in addition to Press.

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

## Logs

Logs are in `%AppData%\MacroGrid\logs\`. Include them when you [open an issue](https://github.com/Deccoyi/macro-grid/issues).
