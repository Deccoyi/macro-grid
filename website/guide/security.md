# Security

Macro Grid is designed for a **home or office network you trust**. It is not hardened for the internet.

::: warning Beta, AI-generated, no warranty
All code, design and documentation were created by an AI assistant and have not been reviewed line by line by a human or security-audited. The software is provided "as is" with no warranty or liability. You use it entirely at your own risk: which devices you pair, which plugins you install and which buttons you press. The installer and the app ask you to accept the [user agreement](https://github.com/Deccoyi/macro-grid/blob/main/installer/license-agreement.txt).
:::

- **Nothing is encrypted.** Traffic is plain `ws://` and `http://` on your network.
- **Never forward port 9820** to the internet. Allow it in the firewall for private networks only (the installer does this).
- **Pairing uses a PIN**, then a per-device token. The PIN works only while the Pairing window is open, for at most five minutes, and for one device. A device that enters too many wrong PINs has to wait, and the PIN is replaced. Tokens are stored in `%AppData%\MacroGrid\devices.json`, encrypted with Windows DPAPI for your Windows account, so a copy of the file on another account or PC is useless (those devices then have to pair again).
- **A paired device can press keys, type text and start programs on your PC.** Pair only devices you trust, and remove old ones.
- **The editor API is only for the PC itself.** The server answers 403 to editor API requests from other machines, so nobody else on your network can change your profiles or read your PIN.
- **Only official, signed C# plugins run** (they have full trust, so the signature is checked every time one loads); plugins by other authors are JavaScript, sandboxed and need approved permissions.
- **Plugins by other authors are not reviewed.** A JavaScript plugin can only send web requests to the addresses it lists, and its permission says whether each is on this PC, your local network or the internet. The `input` permission (pressing keys and typing) works only while you press one of the plugin's buttons, with small limits, and never into a terminal or a system tool; an approved plugin with it can still type up to 200 characters into an ordinary program, so allow it only for plugins you trust.
- **These protections are deterrents, not guarantees.** The limits on plugins make abuse harder but cannot prevent it completely. Install plugins from other authors only from sources you trust, and stay alert to what a plugin asks for.
- **Security log.** Pairings, wrong PINs, removed devices and plugin installs are written to the log files in `%AppData%\MacroGrid\logs`, with the address they came from but never a PIN or token. Logs stay on your PC and are deleted after 14 days (at most 20 MB in total).
- **Your data stays local.** There is no cloud service and no account.
- **One optional connection: the update check.** About every six hours the server asks github.com whether a newer version exists. It sends only a program name and version (`MacroGrid/<version>`), nothing about you or your PC, and it installs nothing until you click "Install now". The downloaded installer is checked against the SHA-256 GitHub reports for it; it is not code-signed, so Windows asks for administrator permission. You can switch the check off in Preferences > General.
- **The phone app has the same kind of optional connection.** At start and about every six hours it asks github.com whether a newer app version exists, sending only `MacroGridClient/<version>`. It installs nothing until you tap, the file is checked against GitHub's SHA-256, and Android installs it only if it is signed with the same key as the installed app (the key stays on the maintainer's computer and is never put on GitHub). Android asks once for permission to install apps and always shows its own confirmation. Downloads use Wi-Fi only unless you allow mobile data, and you can turn the check off in the app's Settings.

To report a vulnerability see [SECURITY.md](https://github.com/Deccoyi/macro-grid/blob/main/SECURITY.md).
