# Security risk assessment

A written look at what could go wrong with Macro Grid's security, what is done about it and what risk is left. It is kept
next to the code so it changes with it. It describes the software as it is; it promises nothing (see `SECURITY.md`: a hobby
project, no support period, no promise of fixes).

**Why it exists.** Macro Grid is free, open source and not monetised, so it is most likely outside the EU Cyber Resilience Act
(Regulation (EU) 2024/2847, recital 18). The project still follows the parts of its essential requirements (Annex I) that are
cheap for a hobby project, and this file plays the role of the risk assessment a manufacturer would keep (Article 13(2), (3)).

Last reviewed: 2026-09-26, server 1.0.1.

## The product

- **Server** (`MacroGrid.exe`): a Windows tray application with a web server on port 9820. It stores profiles, runs actions on
  the PC (key presses, typing text, starting programs, volume, plugin actions) and serves the editor, the browser deck and a
  WebSocket for decks.
- **Editor**: the server's own window, talking to the loopback-only `/api`.
- **Decks**: the Android app (`macro-grid-client`) and any browser on the network at `/deck/`.
- **Plugins**: C# plugins (full trust, in-process) and JavaScript plugins (sandboxed, with declared permissions), from the
  official repository, another author's repository or a folder.
- **Update channels**: the server and the app check GitHub releases; plugins install from GitHub releases.

**Intended use:** a home or office network the user trusts. **Foreseeable misuse:** exposing port 9820 to the internet, using it
on public Wi-Fi, installing untrusted plugins.

## What is worth protecting

| Asset | Why |
|---|---|
| Control of the PC | A paired device or a plugin can press keys, type text and start programs as the user. |
| Device tokens (`devices.json`) | A token is enough to control the PC from the network. |
| Plugin settings | May hold passwords for other software (for example a streaming app's WebSocket password). |
| The release and update channels | A tampered installer, app or plugin would run on every PC that updates. |
| Profiles and preferences | The user's work; not secret, but should not be lost or silently changed. |

## Threats, measures and remaining risk

| # | Threat | Measures | Remaining risk |
|---|---|---|---|
| T1 | Someone on the network pairs a device by guessing the PIN | PIN valid only while the Pairing window is open (15 s lease), at most 5 min, single-use; 5 wrong PINs block an address (30 s, doubling to 15 min); 20 wrong PINs replace the PIN; fixed-time comparison (`PairingService`). Pairing events are logged. | Low. An attacker who can see the traffic can read a PIN (T2). |
| T2 | Someone on the network reads or changes traffic (PIN, token, key presses) | None: traffic is plain `ws://` / `http://`. Documented in `SECURITY.md` and the user guide: trusted networks only, firewall rule for private networks only. | **High on an untrusted network.** Planned: `docs/plans/transport-encryption-plan.md`. |
| T3 | Someone on the network uses the editor API (read the PIN, install or approve plugins) | `/api` answers only the loopback address (`LoopbackGuard`). | Low. |
| T4 | A copy of the data folder (backup, other account) reveals device tokens | Tokens encrypted with DPAPI for the Windows user (`DpapiSecretProtector`); a copy on another account or PC is useless. | Medium against malware running as the same user (DPAPI cannot help there). |
| T5 | Plugin settings reveal passwords of other software | None yet: each plugin writes its own `settings.json` in plain text. | Medium. Planned: a secret-storage helper in the plugin SDK (needs an SDK release). |
| T6 | A malicious or careless plugin | Third-party plugins are marked and need a confirmation that says what they can do (internet, files, programs); JS plugins run in a sandbox with time, memory and statement limits and only the permissions the user approves; official plugins are signed (ECDSA P-256) with a key kept off GitHub. | **High for C# plugins by others**: full trust, cannot be limited. The user decides. |
| T7 | A tampered server installer or update | HTTPS to an allow-list of GitHub hosts, redirects checked, size and SHA-256 compared with what GitHub reports, administrator prompt (`docs/design/auto-update.md`). Release tags are protected; releases are drafts until the owner publishes them. | Medium: the installer is not code-signed, so a compromised GitHub account could ship a matching file and digest. |
| T8 | A tampered phone app update | SHA-256 against GitHub, and Android installs only an APK signed with the same key, which is kept on the owner's PC. | Low. |
| T9 | A vulnerable third-party component | CI and the release workflows stop on a known vulnerable .NET or production npm package; Dependabot security updates and monthly version updates; `dependency-review` on pull requests; an SBOM per release. | Low to medium: unknown vulnerabilities; the Android (Gradle) libraries are not in the client SBOM yet. |
| T10 | A compromised build pipeline | Every action pinned to a commit; default token read-only; NuGet trusted publishing (no stored key); the SDK publish job runs only on protected tags; plugin signing happens on the owner's PC. | Low to medium: an attacker with the owner's GitHub account. The account has 2FA. |
| T11 | Logs fill the disk or leak secrets | Logs never contain a PIN or a token; 14 days, 5 MB a day, 20 MB in total (`LogRetention`); a pairing block is logged once. | Low. Logs contain local network addresses; they stay on the PC. |
| T12 | Data left behind after uninstall | Uninstall keeps `%AppData%\MacroGrid` on purpose (profiles survive a reinstall). | Low (tokens are encrypted). A "remove my data" choice is planned. |

## Security properties (Annex I, part I) at a glance

| Property | State |
|---|---|
| No known exploitable vulnerabilities at release | Checked automatically before each release (T9). |
| Secure by default | Pairing required; editor API loopback-only; firewall rule private networks only; update check on, installation only on request. |
| Security updates | Automatic check, user-confirmed install, can be switched off. |
| Protection from unauthorised access | Pairing (T1), loopback API (T3). |
| Confidentiality at rest | Device tokens encrypted (T4); plugin secrets not yet (T5). |
| Confidentiality and integrity in transit | **Missing on the local network (T2).** Update downloads use HTTPS and digests. |
| Data minimisation | No telemetry, no account. Only the update check leaves the network, with the program name and version. |
| Limit attack surface | One port; editor API local only; plugins opt-in. The server listens on all interfaces (needed for decks). |
| Security event logging | Yes (T11). |
| Secure data removal | Partly (T12). |

## Technical file (where things are)

| Item | Where |
|---|---|
| Product description and design | `README.md`, `docs/architecture.md`, `docs/design/` |
| Security model and user information | `SECURITY.md`, `docs/architecture.md#security-model`, the website's security page |
| Vulnerability handling and reporting | `SECURITY.md` (private vulnerability reporting on GitHub) |
| Software bill of materials | Attached to each release (`MacroGrid-Server-<version>-*.cdx.json`), made by `scripts/sbom.ps1` |
| Vulnerability check | `scripts/check-vulnerabilities.ps1`, `.github/workflows/ci.yml`, `.github/workflows/release.yml` |
| Tests | `tests/MacroGrid.Tests` (including `PairingServiceTests`, `DeviceStoreTests`, `LogRetentionTests`) |
| Third-party components and licenses | `THIRD_PARTY_NOTICES.md`, `licenses/` |
| Support period | None; see `SECURITY.md` |

Review this file when the network protocol, pairing, plugin loading, the update or release process, or storage of anything secret
changes.
