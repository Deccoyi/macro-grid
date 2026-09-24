# Security policy

## What to expect from this software

Macro Grid is an alpha-stage project written by an AI assistant and not independently audited (see the [README](README.md)). It is provided "as is",
without warranty of any kind (see the [MIT license](LICENSE)); you use it at your own risk.

### Threat model: a trusted local network only

Macro Grid is designed for a home or office network you trust. It is **not** hardened for the internet.

- Traffic between the server and a deck is not encrypted (plain HTTP and WebSocket on port 9820), and the server listens on all network interfaces.
  Do not forward the port to the internet, and allow it in the firewall for private networks only.
- **Pairing exists.** A new device must present the six-digit PIN (valid for five minutes, shown in the editor's Pairing window, also as a QR code).
  The server then issues a device token that the device uses from then on. Tokens are stored in plain text in `%AppData%\MacroGrid\devices.json`.
  Because the PIN and the token travel unencrypted, they protect against casual access, not against an attacker who can watch the network.
- A paired device can press keys, type text and start programs on the PC. Pair only devices you trust and remove the ones you no longer use.
- The editor API is reachable only from the server's own computer.
- C# plugins run with full trust and can do anything the server can; install only plugins you trust. JavaScript plugins run in a sandbox and only
  get the permissions you allow.

The full model, including what is and is not protected, is in [docs/architecture.md](docs/architecture.md#security-model).

## Reporting a vulnerability

Please report security problems **privately**, not in a public issue. Use GitHub's private vulnerability reporting: open the **Security** tab of this
repository, then **Advisories**, then **Report a vulnerability**. (Private vulnerability reporting will be enabled on the repository when it is
made public.)

If that channel is not available, write to the project mailbox, macrogrid.app@gmail.com, and put no more than "security report" in the subject line.

Helpful details: what is affected, the steps to reproduce, and what an attacker on the same network (or with a paired device, or with an installed
plugin) could do.

This is a small project maintained in spare time, so there is no guaranteed response time and no bug bounty, but reports are taken seriously and
credited if you wish.

## Supported versions

Only the latest release (or, before the first release, the `dev` branch) receives fixes. The project is in alpha, so expect changes.
