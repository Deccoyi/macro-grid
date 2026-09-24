# The Macro Grid ecosystem

Macro Grid is three things that work together. You need the first one; the others are optional.

## The server and editor (Windows)

The heart of it. It runs on your Windows PC in the notification area, keeps your decks, runs the actions when you press a button and hosts the **editor** where you design everything. Every other part talks to it.

[Install it](/guide/install) · [The editor](/guide/editor)

## The phone and tablet app (Android)

Shows your deck full screen and sends your touches to the PC. It pairs with a QR code, reconnects by itself, remembers the last layout when the connection drops and has a kiosk mode. If you do not have an Android device, a browser on any device can show the deck instead.

[The phone app](/guide/phone-app) · [Pair a device](/guide/pairing)

## Plugins

Plugins teach Macro Grid new tricks without a restart:

- **OBS**: switch scenes, go live, record, mute sources and show the stream timer and stats on your deck.
- **PLC Icons**: a pack of ladder-logic symbols for the icon picker.
- **Your own**: small sandboxed JavaScript plugins, or full C# ones. See [For developers](/developers/).

[Using plugins](/guide/plugins)

## How they connect

```
Phone or browser  <-- your local network -->  Macro Grid on your PC  <-->  Plugins (OBS, ...)
```

Everything stays on your own network: no cloud and no account. See [Security](/guide/security).
