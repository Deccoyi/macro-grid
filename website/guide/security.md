# Security

Macro Grid is designed for a **home or office network you trust**. It is not hardened for the internet.

::: warning Alpha, AI-generated, no warranty
All code, design and documentation were created by an AI assistant and have not been reviewed line by line by a human or security-audited. The software is provided "as is" with no warranty or liability. You use it entirely at your own risk: which devices you pair, which plugins you install and which buttons you press. The installer and the app ask you to accept the [user agreement](https://github.com/Deccoyi/macro-grid/blob/main/installer/license-agreement.txt).
:::

- **Nothing is encrypted.** Traffic is plain `ws://` and `http://` on your network.
- **Never forward port 9820** to the internet. Allow it in the firewall for private networks only (the installer does this).
- **Pairing uses a PIN** valid for five minutes, then a per-device token. Tokens are stored in plain text in `%AppData%\MacroGrid\devices.json`.
- **A paired device can press keys, type text and start programs on your PC.** Pair only devices you trust, and remove old ones.
- **The editor API is only for the PC itself.** The server answers 403 to editor API requests from other machines, so nobody else on your network can change your profiles or read your PIN.
- **C# plugins have full trust**; JavaScript plugins are sandboxed and need approved permissions.
- **Your data stays local.** There is no cloud service and no account.
- **One optional connection: the update check.** About every six hours the server asks github.com whether a newer version exists. It sends only a program name and version (`MacroGrid/<version>`), nothing about you or your PC, and it installs nothing until you click "Install now". The downloaded installer is checked against the SHA-256 GitHub reports for it; it is not code-signed, so Windows asks for administrator permission. You can switch the check off in Preferences > General.
- **The phone app has the same kind of optional connection.** At start and about every six hours it asks github.com whether a newer app version exists, sending only `MacroGridClient/<version>`. It installs nothing until you tap, the file is checked against GitHub's SHA-256, and Android installs it only if it is signed with the same key as the installed app (the key stays on the maintainer's computer and is never put on GitHub). Android asks once for permission to install apps and always shows its own confirmation. Downloads use Wi-Fi only unless you allow mobile data, and you can turn the check off in the app's Settings.

To report a vulnerability see [SECURITY.md](https://github.com/Deccoyi/macro-grid/blob/main/SECURITY.md).
