# The phone app

The Android app draws the deck full screen and sends your touches to the server. Source and releases: [macro-grid-client](https://github.com/Deccoyi/macro-grid-client).

- Android 7.0 (API 24) or newer, on the same network as the server.
- The app does nothing without a running Macro Grid server.
- The app's screens are currently in Turkish only.

## Install

1. Install and start the [server](/guide/install) first.
2. Download the APK from the [phone app download page](https://deccoyi.github.io/macro-grid-client/download) and open it on the phone. Allow installing from your browser or file manager when Android asks.

## Connect

- Scan the QR code from the editor's Pairing window, **or**
- enter the server address and the PIN. The app remembers servers you used; switch, delete or add one from the drawer.

## Using the deck

![A deck on a phone](/img/deck-phone.png)

| You do | What happens |
|---|---|
| Tap a button | Press and release are sent to the server. |
| Hold | Long press. |
| Tap twice | Double tap. |
| Drag a slider or knob | The value is sent; if it is tied to a variable it also follows changes made elsewhere. |
| Swipe left or right with two fingers | Change page. It works anywhere on the deck, also over a slider or knob. One finger never changes page. |
| Pull from the edge (or use the handle) | Open the **profile drawer**. |

The drawer lists profiles, saved servers and the **lock** switch. While the lock is on, [automatic profile switching](/guide/auto-switch) pauses; choosing a profile by hand always works.

## Settings

- **Kiosk mode** hides the status and navigation bars (on by default).
- **Orientation lock**.
- **Keep awake**: the screen stays on while a profile is shown.

## When the connection drops

The app reconnects with a growing wait, shows the last layout with an "offline, cached" badge, and caches icons so reconnecting is quick. If an action fails on the PC, you see a short red error toast.
