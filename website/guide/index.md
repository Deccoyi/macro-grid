# Quick start

From nothing to a working button on your phone in about ten minutes.

::: warning Alpha software
Macro Grid is in public alpha and was written entirely by an AI assistant. Features and file formats can change between versions. Read [Security](/guide/security) before you pair a device.
:::

## What you need

- A Windows 10 or 11 PC (the server is Windows only).
- A phone or tablet on the **same network** with the [Android app](/guide/phone-app), or just a browser.

## 1. Install the server

Download `MacroGrid-Setup-<version>.exe` from the [Releases page](https://github.com/Deccoyi/macro-grid/releases) and run it. Details: [Install the server](/guide/install).

Macro Grid starts as a **tray icon** near the clock. Double-click it (or use its menu) to open the editor.

## 2. Pair your phone

1. In the editor, click **Pairing**. It shows a six-digit PIN and a QR code, valid while the window is open (at most five minutes).
2. On the phone, open the app and scan the QR code. You can also type the PC address and the PIN.
3. The phone shows your profile. Next time it reconnects on its own.

More: [Pair a device](/guide/pairing).

## 3. Make your first button

1. Click **Add widget** and choose **Button**. It lands on a free cell of the grid.
2. Select it and set its **Text**, for example `CPU {system.cpu|0}%`. The `{...}` part is a live value.
3. Under **Actions**, on **Press**, click **+ Add action** and pick **Shortcut**. Click the field and press your combination, for example `Ctrl+Shift+S`.
4. Click **Save**. The phone updates right away. Press the button and the PC receives the shortcut.

::: tip Several actions, one macro
Add more actions to the same event and they run one after another. Put a **Delay** between them if a program needs time.
:::

## Where next

- Learn the tools: [The editor](/guide/editor), [Widgets](/guide/widgets), [Actions](/guide/actions).
- Follow a tutorial: [a volume slider](/tutorials/volume-slider) or [a streaming deck with OBS](/tutorials/obs-deck).
- Something not working? [Troubleshooting](/guide/troubleshooting).
