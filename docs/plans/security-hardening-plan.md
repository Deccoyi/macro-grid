# Security hardening plan

Status: **planned, not started; waiting for the owner's decision.** Repositories: `macro-grid` (all parts), `macro-grid-client`
(part A), `macro-grid-plugin` (part B). Touches the security model: update `../architecture.md` and
`../design/security-risk-assessment.md` with each part.

The risk assessment (`../design/security-risk-assessment.md`) lists what is left after the pairing limits, the security log,
the encrypted device tokens, the vulnerability checks and the SBOM. These are the three larger items. None is promised; this is
a hobby project.

## A. Encrypted connection on the local network (threat T2)

Today PINs, tokens and key presses cross the network in plain `ws://` and `http://`.

**Option 1: TLS with a certificate the server makes itself (recommended).**
- Server: on first start, create an ECDSA P-256 certificate for the PC, store it in `%AppData%\MacroGrid` with the private key
  encrypted by DPAPI (`ISecretProtector`), and serve `wss://` / `https://` on a second port (for example 9821) next to 9820.
- Pairing QR: add the certificate's SHA-256 fingerprint (`&fp=`). The phone app pins that fingerprint for this server, so no public
  certificate authority is needed and a man in the middle is refused.
- Phone app: the WebView's own WebSocket cannot pin a certificate, so the connection moves to a small native WebSocket (Capacitor
  plugin) that checks the fingerprint. This is most of the work.
- Browser deck: a self-made certificate makes the browser warn once. Keep `http://.../deck/` as an explicit, labelled choice.
- Migration: a preference "Allow unencrypted connections", on while older apps exist, then off by default in a later version.
  The WebSocket messages do not change.
- Cost: TLS on a handful of local connections is negligible for the resource budget.

**Option 2: encrypt inside the existing WebSocket.** A key exchange bound to the pairing PIN (a PAKE) gives each device a key;
every message is encrypted with it. Works in any browser without certificate warnings, but it is custom cryptography in a hobby
project, which is harder to get right and to review. Not recommended.

Open questions: a second port or switch 9820 to TLS; how long to keep plain connections; whether the browser deck needs TLS at all.

## B. Secret storage for plugins (threat T5)

Plugins write their own `settings.json`, so a password field (for example the OBS plugin's) is stored in plain text.

- SDK (`macro-grid`): add an optional `IPluginSecrets` to `IPluginHost` with `Protect(string)` / `Unprotect(string)`, backed by the
  host's `ISecretProtector` (DPAPI). Additive, so a MINOR version (`1.x`) of the one server and SDK version.
- Settings pages: let `SettingFieldKind.Password` values be stored protected by the host when the plugin opts in, so plugins do
  not have to handle it themselves.
- Plugins (`macro-grid-plugin`): OBS stores its password through it and converts an existing plain value on first load; raise
  `macroGrid` in its `plugin.json` to the SDK version that has it.
- Needs an SDK release on NuGet before the plugin can build against it (the owner tags it; `../guides/release.md`).

## C. Remove my data on uninstall (threat T12)

Uninstall keeps `%AppData%\MacroGrid` on purpose. Add a question at the end of the uninstall: "Also delete your profiles,
paired devices, plugins and logs?" (default: No).

- The uninstaller runs as administrator. `{userappdata}` then points at the account that approved the prompt, which may not be the
  person who used Macro Grid (the same trap as the agreement hash in `../design/agreement-acceptance.md`). Resolve the folder
  of the person who started the uninstall, or delete only when both are the same account and say so otherwise.
- Must never run on an update (`/UPDATE`) or a silent uninstall without an explicit switch.
- Needs a test on a clean PC: install, pair, uninstall with Yes and with No, and uninstall from another Windows account.

## Not in this plan

- **Code signing of the installer (threat T7):** needs a code-signing certificate or a signing service for open-source projects;
  that is the owner's account and paperwork. See `../guides/release.md`, "Not done yet".
- **Android libraries in the client SBOM:** a CycloneDX Gradle plugin in the client build; small, can be done on its own.
