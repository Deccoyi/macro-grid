# Macro Station

Turn a phone or tablet on your local network into a customizable macro deck for your Windows PC, like a hardware macro keypad you design yourself.
You lay out buttons, toggles, sliders and knobs on a grid in the editor; the deck shows live values from the PC (CPU, RAM, the time, OBS stream
duration, ...) and presses keys, types text, opens programs, changes the volume and controls other software through plugins.

> ## This project was written entirely by an AI assistant
>
> All code, design and documentation in this repository were written by an AI assistant (Claude) at a user's direction. It has not been reviewed line by line by a
> human, security-audited or certified for production use.
>
> **No warranty of any kind.** The software is provided "as is", without warranty of any kind, express or implied, including but not limited to
> merchantability, fitness for a particular purpose and non-infringement. You use it entirely at your own risk. See [LICENSE](LICENSE) (MIT).

This repository is the **server and editor**. The phone and tablet app is in [macro-station-client](https://github.com/Deccoyi/macro-station-client), and
plugins are in [macro-station-plugin](https://github.com/Deccoyi/macro-station-plugin). The three are versioned independently.

## What it does

- **The server** runs on Windows as a tray application. It stores profiles, talks to the deck over a WebSocket (port 9820) and runs the actions on the PC:
  key combinations, typing text, opening programs and URLs, switching pages and profiles, delays, volume and mute, and whatever plugins add. Several actions
  bound to one event run in order, which makes a macro.
- **The editor** opens in the server's own window. You design pages on a grid by dragging widgets, style them (colors, icons, animation, custom CSS),
  assign actions to press, long press and double tap, and preview the result live.
- **Live values.** Widgets show text such as `CPU {system.cpu|0}%`, and a widget's color, animation, icon or text can change with a value through simple rules:
  "if CPU is above 80 blink red".
- **Sliders and knobs** are two-way: they can control the Windows volume and follow it when it changes elsewhere.
- **Several devices and profiles.** Each paired device can show a different profile, and a device can follow the active window on the PC (a media player
  comes to the front, the deck switches to its profile).
- **Plugins.** OBS control, icon packs and your own, in C# or in sandboxed JavaScript; installed and reloaded without a restart.
- **Profile files.** Export a profile as a single `.msprofile` file and import it elsewhere.
- **Browser deck.** Any browser on the network can act as a deck at `http://<PC address>:9820/deck/`.

All communication stays on your local network.

## Requirements

- Windows 10 or 11.
- The [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (part of current Windows).
- A phone or tablet on the same network with the [Android app](https://github.com/Deccoyi/macro-station-client), or any browser.

There are no published releases yet; build from source (below). A Windows installer definition is included and described in [docs/release.md](docs/release.md).

## Getting started

1. Build and start the server (see below). It appears as a tray icon; the menu opens the editor.
2. Open the editor's **Pairing** window: it shows a six-digit PIN and a QR code, valid for five minutes.
3. On the phone, open the app, then scan the QR code (or enter the PC's address and the PIN). The device is paired and shows the profile.
4. Design your pages in the editor. Saving updates connected devices immediately.

The phone app's screens and the browser deck are currently in Turkish; the editor is available in Turkish (default) and English (Preferences).

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/) 20 or newer.

```powershell
cd editor;    npm install; npm run build; cd ..
cd webclient; npm install; npm run build; cd ..
Copy-Item editor\dist\*    src\MacroStation.Host\wwwroot\editor -Recurse -Force
New-Item -ItemType Directory -Force src\MacroStation.Host\wwwroot\deck | Out-Null
Copy-Item webclient\dist\* src\MacroStation.Host\wwwroot\deck -Recurse -Force
dotnet run --project src/MacroStation.Host
```

Run the tests with `dotnet test`. More in [docs/development.md](docs/development.md).

## Security

Macro Station is designed for a home or office network you trust, not for the internet.

- Traffic is not encrypted. The server listens on all network interfaces on port 9820; do not forward the port, and allow it in the firewall only for
  private networks.
- A device must be paired with the PIN, and tokens are stored in plain text in `%AppData%\MacroStation\devices.json`.
- A paired device can press keys, type text and start programs on your PC. Pair only devices you trust.
- The editor API is reachable only from the server's own computer.
- C# plugins have full trust and can do anything the server can; install only ones you trust. JavaScript plugins are sandboxed.

Details are in [docs/architecture.md](docs/architecture.md#security-model). To report a security problem, see [SECURITY.md](SECURITY.md).

## Documentation

- [Architecture](docs/architecture.md): how it is put together, the protocol and the security model
- [Development](docs/development.md): building, running, testing and the pitfalls
- [Roadmap](docs/roadmap.md): what is done and what is next
- [Releasing](docs/release.md) and [versioning](docs/versioning.md)
- [Design notes](docs/design/): automatic profile switching, layout patches and cached assets, JavaScript plugins
- [UI guidelines](docs/ui-guidelines.md) and [color system](docs/color-bible.md)
- [Changelog](docs/CHANGELOG.md) (short) and [developer changelog](docs/CHANGELOG-developer.md)
- Writing plugins: `docs/plugin-authoring.md` in the [plugin repository](https://github.com/Deccoyi/macro-station-plugin)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE). Third-party components are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
