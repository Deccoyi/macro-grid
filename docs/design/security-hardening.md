# Security hardening

Status (checked 2026-09-29): **all parts released** (server 1.1.0/1.2.0, plugin repo `main` — OBS 0.3.0 uses part B, phone
app `client-v0.3.0` has part A's certificate pinning). What was part D (a native C# plugin's install-time warning) is done
too — see "Native plugins" below; it was never a permission mechanism to design, just a warning screen, and closing it
does not need a decision. (2026-09-29: that folder warning was replaced by the rule that only official, signed C# plugins load at all; see
`./plugin-distribution.md`, section 10, and `../architecture.md`.)
Repositories: `macro-grid` (all parts), `macro-grid-client` (part A), `macro-grid-plugin` (part B). Touches the security
model: update `../architecture.md` and `./security-risk-assessment.md` with each part.

The risk assessment (`./security-risk-assessment.md`) lists what is left after the pairing limits, the security log,
the encrypted device tokens, the vulnerability checks and the SBOM. These are the three larger items. None is promised; this is
a hobby project.

## A. Encrypted connection on the local network (threat T2)

Status (checked 2026-09-28): **released, both sides.** Option 1 below is what's built: server side shipped in Macro Grid 1.1.0/1.2.0,
client side (certificate pinning) shipped in the phone app `client-v0.3.0`.

Today PINs, tokens and key presses cross the network in plain `ws://` and `http://`.

**Option 1: TLS with a certificate the server makes itself (recommended, built).**
- Server: on first start, create an ECDSA P-256 certificate for the PC (`ServerCertificateProvider`), store it in
  `%AppData%\MacroGrid\tls-cert.dat` with the private key encrypted by DPAPI (`ISecretProtector`), and serve `wss://` /
  `https://` on a second port, **9821** (`ServerApp.TlsPort`), next to 9820. Both ports stay open; nothing about 9820 changes.
  Checked 9821 against IANA's full registry (not registered) and a general search (no common Windows software defaults to
  it) before settling on it; a real collision would fail loudly at startup (port already in use) rather than silently.
- Pairing QR: the certificate's SHA-256 fingerprint, lowercase hex, no separators, 64 characters
  (`Convert.ToHexStringLower(cert.GetCertHash(HashAlgorithmName.SHA256))`), as `fp=` — final, implemented. `tlsPort` is also in
  the QR text and as its own field in the `/api/pairing/qr` response. The client pins that fingerprint for this server, so no
  public certificate authority is needed and a man in the middle is refused. No `hello`/`welcome` field says whether TLS is
  "required" — the presence of `fp` in the scanned QR is the whole signal: a client that understands it pairs over
  `wss://host:tlsPort` from then on, one that doesn't keeps using `ws://host:port` like today.
- Phone app: the WebView's own WebSocket cannot pin a certificate, so the connection moves to a small native WebSocket (Capacitor
  plugin, `PinnedSocketPlugin.java`) that checks the fingerprint. Shipped in `client-v0.3.0`.
- Browser deck: a self-made certificate makes the browser warn once, and a browser cannot pin a fingerprint at all the way the
  phone app does — `http://.../deck/` on plain 9820 stays the only supported way to reach it. Not revisited by the decision
  below: the deck was never a candidate for "encrypted by default," only the phone app pairing/data connection was.
- Cost: TLS on a handful of local connections is negligible for the resource budget.

**Decision (owner, relayed 2026-09-27): plain `ws://` stays available by default, not switched off now.** The instinct was
"why allow unencrypted at all when encrypted is available" (CRA leans toward encrypted-by-default), weighed against what
would actually break if the "Allow unencrypted connections" preference defaulted to off today:
- The browser deck cannot use TLS at all here (previous bullet) — defaulting to "encrypted only" would make the deck
  unreachable by default, not just less convenient.
- Every phone already paired before this shipped runs an app build with no TLS support yet; defaulting to off would silently
  break every existing pairing the moment this update installs, not just new ones.

Neither of those is a security argument for keeping plain `ws://` — they are compatibility ones, and they resolve themselves
as the phone app and browser-deck stories above are finished. So the shape is: **encrypted by construction going forward, not
by a runtime toggle.** From the moment a phone build understands `fp`, every *new* pairing already happens over `wss://`,
because the QR always carries `fp` — there was never a way to opt a new pairing out of it, and no toggle is planned to add
one. Plain `ws://` is what's left over for what can't do better yet (old paired devices, the deck), not a co-equal permanent
option. "Prefer wss:// when the client supports it" was considered as a live per-connection negotiation and set aside: the two
fixed ports make that meaningless — which port a client uses is decided once, when it reads the QR, not renegotiated per
connection.
- The "Allow unencrypted connections" preference is built (`AppPreferences.AllowUnencrypted`, Preferences window, default **on** for the
  compatibility reasons above). Off, the plain port answers only this computer (the editor needs it) and refuses every other device with 403
  (`PlainConnectionPolicy`); the encrypted port is unaffected, and open connections stay until they end.
- Cutover point, concrete rather than open-ended: default **off** starting the next MAJOR version (2.0.0), once the Android
  client's TLS support has shipped for at least one MINOR release (so "old app, can't do TLS" stops being the common case)
  and the browser deck's story is settled one way or another. Before that MAJOR, a person who wants to require TLS today can
  already do so by relying only on `wss://`-capable clients and ignoring 9820 — the port doesn't have to be closed for that,
  since nothing forces a client to use it.

**Option 2: encrypt inside the existing WebSocket.** A key exchange bound to the pairing PIN (a PAKE) gives each device a key;
every message is encrypted with it. Works in any browser without certificate warnings, but it is custom cryptography in a hobby
project, which is harder to get right and to review. Not recommended.

All three open questions above (port, how long to keep plain connections, the deck) are settled by the decision above. The
"Allow unencrypted connections" preference is implemented.

## B. Secret storage for plugins (threat T5)

Plugins write their own `settings.json`, so a password field (for example the OBS plugin's) is stored in plain text.

Status (checked 2026-09-28): **`macro-grid` side and the OBS plugin are released.** `IPluginHost.Secrets` (`IPluginSecrets`,
`Protect(string)` / `Unprotect(string)`) shipped in server/SDK 1.1.0, backed by the same `ISecretProtector` (DPAPI) the device
store uses — a shared DI singleton (`AddHostStores`) instead of being constructed twice. Tests in `PluginSecretsTests`. The SDK
1.1.0 NuGet release happened, and `WebSocketBridge For OBS` 0.3.0 (`macroGrid: 1.1.0`) stores its password through it, converting
an existing plain value on first load — released on `macro-grid-plugin`'s `main`.

Still open:
- Settings pages: let `SettingFieldKind.Password` values be stored protected by the host when the plugin opts in, so plugins do
  not have to handle it themselves the way OBS does today.

## C. Remove my data on uninstall (threat T12)

Status (checked 2026-09-28): **done, released.** `installer/MacroGrid.iss` asks at uninstall: "Also delete your profiles,
paired devices, plugins and logs (`%AppData%\MacroGrid`)?", default No (`MB_DEFBUTTON2`). Never asks on a silent uninstall
(`UninstallSilent`); the code only runs in the uninstaller, so a Setup `/UPDATE` run never touches it. Tested by hand on this
machine: built with `scripts\publish.ps1` + `installer\build-installer.ps1` (Inno Setup 6), installed, uninstalled with No
(data kept), reinstalled, uninstalled with Yes (data deleted) — confirmed by the owner.

- `{userappdata}` resolves for the uninstaller's own process token, the same trap as the agreement hash in
  `./agreement-acceptance.md`: only "Run as a different user" breaks the assumption that it is the person who started
  the uninstall. Not solved further (same as the agreement hash), the prompt names the exact folder instead.
- Not tested: uninstalling from a second Windows account than the one that installed it, or on a genuinely clean PC/VM (only
  this dev machine, which already had real paired devices and an OBS password — both confirmed gone after a "Yes" uninstall).

## Native plugins have no permission gate, by design (was part D)

A native (C#) plugin's manifest has no `permissions` field at all — `PluginManager.Loading.cs`'s approval gate only runs
`if (manifest.Kind == PluginKind.Js)`. This is not a gap to close: a JS plugin's permissions are enforced by its sandbox,
but a native plugin runs in-process with full trust, so a declared permission on it could only ever be a label the plugin
chooses to be honest about, not something the host can check or stop — not worth building for a false sense of security.

Status (checked 2026-09-29): **done, released.** Instead of a permission mechanism, installing a native plugin from a
folder (there is no "official source" to exempt for a local folder, unlike Discover/link installs) shows a clear,
danger-styled warning first: it runs with full access to this computer and Macro Grid cannot check or limit it, so only
install one from a folder you trust. See `PluginApi.cs`'s `/plugins/install/browse` + `/plugins/install/confirm` and
`PluginsWindow.tsx`'s `install()`.

JS plugin approval used to happen after install (the plugin sat in a `NeedsApproval` state; "Allow and Enable" only
appeared once already installed, with no confirmation at the point of clicking Install). Moved before install instead:
the editor shows the declared permissions and asks to confirm as part of the browse/inspect step, before the install call
runs; on success it approves right away so there is no separate second "Enable" click. Applies to all three install paths
(folder, catalog, pasted link).

## E. No Origin check on the loopback API (threat T13, found while building part B)

Status (checked 2026-09-28): **done, released** (server 1.1.0). `OriginGuard.IsAllowed` (`MacroGrid.Core.Devices`) refuses an `/api` request
whose `Origin` header is present and is not one of the editor's own origins (`http://localhost:9820`, `http://127.0.0.1:9820`,
`http://[::1]:9820`, and the Vite dev servers that proxy to it, `:5190` and `:5192`); a request with no `Origin` header at all
(curl, a native app) is unaffected, since only a browser sends it. Wired into `ServerApp.cs` right after `LoopbackGuard`, same
shape, same 403. Tests: `OriginGuardTests`. See `./security-risk-assessment.md`, T13.

`/ws` (the WebSocket the deck and phone use) is not covered: `hello` already requires a PIN or a device token, so an unpaired
page gets nothing from it either way, and adding an Origin check there was judged not worth the complexity.

## Not in this plan

- **Code signing of the installer (threat T7):** needs a code-signing certificate or a signing service for open-source projects;
  that is the owner's account and paperwork. See `../guides/release.md`, "Not done yet".
- **Android libraries in the client SBOM:** a CycloneDX Gradle plugin in the client build; small, can be done on its own.
