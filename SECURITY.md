# Security policy

## What to expect from this software

This software was created entirely by AI tools, is alpha-stage and has not been independently audited or security-reviewed (see the [README](README.md)). It is provided "as is", without warranty of any kind, and the authors and contributors accept no responsibility or liability for it, including for security problems and their consequences (see the [MIT license](LICENSE)). You use it entirely at your own risk. Security reports are welcome, but they create no obligation to fix and are not a promise of support or of a response time.

### Threat model: a trusted local network only

Macro Grid is designed for a home or office network you trust. It is **not** hardened for the internet.

- Traffic between the server and a deck is not encrypted (plain HTTP and WebSocket on port 9820), and the server listens on all network interfaces.
  Do not forward the port to the internet, and allow it in the firewall for private networks only.
- **Pairing exists.** A new device must present the six-digit PIN shown in the editor's Pairing window (also as a QR code). A PIN is valid only
  while that window is open, for at most five minutes, and pairs one device. An address that sends five wrong PINs has to wait, longer each time,
  and after twenty wrong PINs the PIN is replaced. The server then issues a device token that the device uses from then on. Tokens are stored in
  `%AppData%\MacroGrid\devices.json`, encrypted with Windows DPAPI for your Windows account, so a copy of the file on another account or PC is useless (those devices then have to pair again). DPAPI does not protect against other programs running as you.
  Because the PIN and the token travel unencrypted, they protect against casual access, not against an attacker who can watch the network.
- A paired device can press keys, type text and start programs on the PC. Pair only devices you trust and remove the ones you no longer use.
- The editor API is reachable only from the server's own computer.
- C# plugins run with full trust and can do anything the server can; install only plugins you trust. JavaScript plugins run in a sandbox and only
  get the permissions you allow.
- **Plugins by other authors** are not reviewed by this project. A C# plugin can read your files, start programs and connect to the internet and
  send data, and Macro Grid cannot limit or check that. A JavaScript plugin can only send web requests to the exact addresses it lists, and the
  permission it asks for says whether each one is on this computer, your local network or the internet.
- **Security log.** Pairings, wrong PINs, removed devices and plugin installs and permissions are written to the log files in
  `%AppData%\MacroGrid\logs` with the address they came from (never a PIN or a token). The logs stay on this computer and are kept for 14
  days, at most 5 MB a day and 20 MB in total; older ones are deleted automatically.

The full model, including what is and is not protected, is in [docs/architecture.md](docs/architecture.md#security-model).

### What a release contains

Every server release has software bills of materials (SBOM) attached: CycloneDX JSON files (`MacroGrid-Server-<version>-*.cdx.json`) that list
the .NET and npm components shipped in `MacroGrid.exe`, the editor and the browser deck. A release is only built when none of those components
has a known vulnerability at that moment (npm: moderate or higher), and every pull request runs the same check.

## Reporting a vulnerability

Please report security problems **privately**, not in a public issue. Use GitHub's private vulnerability reporting: open the **Security** tab of this
repository, then **Advisories**, then **Report a vulnerability**.

If that channel is not available, open a normal issue that says only that you have a security report, with no details, and a maintainer will
arrange a private way to receive it.

Helpful details: what is affected, the steps to reproduce, and what an attacker on the same network (or with a paired device, or with an installed
plugin) could do.

## This is a hobby project

Macro Grid is a hobby project maintained in spare time, not a full-time job or a commercial product. Security reports are read and the
maintainer will try to fix real problems, but there is no guaranteed response time, no guaranteed fix, no support schedule and no bug
bounty. Fixes land when there is time for them. If that is not acceptable for how you use the software, do not rely on it.
Reporters are credited if they wish.

## How fixes are announced

This is a hobby project, so nothing here is a promise. If a reported vulnerability gets fixed, the fix may be described in a GitHub
security advisory on this repository and under "Security" in the changelog.

## Supported versions

There is no support period and no promise of fixes: Macro Grid is a hobby project maintained in spare time. If a fix is made, it only goes
into a new release; older versions are not updated. Before the first release, fixes land on the `dev` branch. The project is in alpha, so expect changes.
