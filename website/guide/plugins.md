# Plugins

Plugins add **actions**, **variables**, **settings pages**, **icon packs** and **widgets** for your deck. Their **status bar items** are shown only when you switch them on. They are installed, reloaded and removed while the server runs, with no restart.

Writing your own plugin, the manifest, the SDKs and tutorials are all on the **[plugin documentation site](https://deccoyi.github.io/macro-grid-plugin/)**. Source and releases: [macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin).

## Install a plugin

![The Plugins window with OBS and PLC Icons installed](/img/plugins.png)

1. Get a plugin folder: unzip a release. A plugin folder contains `plugin.json`. (An official C# plugin you built yourself is refused; only a development build of the server loads one.)
2. In the editor open **Plugins → Manage Plugins…** and choose **Install from Folder…**.
3. The plugin loads immediately. A JavaScript plugin first shows the permissions it wants and only runs after you click **Allow and enable**.

Use the **On / Off** switch in the Plugins window to stop a plugin without removing it. Use **Reload** after changing a plugin, the gear button for its **Settings**, and **Remove** to delete it. Plugins live in `%AppData%\MacroGrid\plugins\<id>\`.

::: danger Trust
Only the **official C# plugins** run: a C# plugin has full access, like the server itself, so Macro Grid loads one only when it carries a valid official signature, checked every time it loads. Plugins by other authors are **JavaScript plugins**, which are sandboxed and can only do what you approve. Macro Grid does not review them: install only ones you trust.
:::

::: warning Install plugins only from sources you trust
Macro Grid limits what a JavaScript plugin can do: you approve its permissions, keyboard input works only while you press one of its buttons, and some targets are refused. These limits only make abuse harder; they **cannot prevent it completely**. Install third-party plugins only from sources you trust, read the permissions before you allow them, and be extra careful with a plugin that can press keys and type (`input`) or send web requests to the internet.
:::

## Permissions

A JavaScript plugin can ask for these permissions:

| Permission | What it allows |
|---|---|
| `variables` | Read variables |
| `actions` | Add actions you can bind to buttons |
| `input` | Press keys and type text, only while you press one of its buttons |
| `storage` | Keep a small amount of its own data |
| `notify` | Show short notices in the tray |
| `http:<host>` | Send web requests to one address |
| `ws:<host>:<port>` | Keep a live connection open to one address |

The Plugins window lists each permission with a tick box. Untick one and the plugin keeps running without it. A plugin that stopped after you switched a permission off can be switched back on in the same window, without installing it again. An update that asks for new permissions waits until you approve them.

## Plugin widgets

A plugin can draw its own widgets, such as a gauge. They appear in the Toolbox under the plugin's name. A plugin widget runs in a closed box with no network access, so a broken one cannot harm the rest of the deck. If a plugin keeps failing, Macro Grid switches it off, and you can turn it on again in the Plugins window.

## Discover

The **Discover** tab of the Plugins window lists plugins from the official source. **Add source…** adds another GitHub repository that publishes a `macrogrid-index.json`. Install and update plugins from there.

- **Go back:** a plugin's page can put back the version before the one you have. Macro Grid asks first.
- **Stay on this version:** stops update offers for that plugin. Safety warnings still appear.
- **Withdrawn versions:** a publisher can withdraw a version. It cannot be installed. If you already have it, you are asked to update or remove it; it keeps running until you do.
- **Unsafe versions:** if the official safety list switches off a version, Macro Grid stops it and says why.

## Official plugins

### OBS {#obs}

Controls OBS Studio over its built-in WebSocket (OBS 28 or newer). It adds **22 actions** (scenes, studio mode, streaming, recording, virtual camera, replay buffer, audio mute and volume, scene item visibility, text sources) and about **45 live `obs.*` variables** (stream duration, dropped frames, recording state, FPS, CPU and more).

1. In OBS: **Tools → WebSocket Server Settings**, enable the server (set a password if you like).
2. In Macro Grid: **Plugins**, click the gear next to OBS, turn **Enabled** on, and enter the server, port (default 4455) and password. It connects within moments.
3. To see the connection state in the editor's status bar, switch **Status bar** on for OBS in the Plugins window.

![The OBS plugin settings form](/img/obs-settings.png)

Try it: [A streaming deck with OBS](/tutorials/obs-deck). Details: [OBS guide](https://deccoyi.github.io/macro-grid-plugin/guides/obs-plugin).

### PLC Icons {#plc-icons}

An icon pack with 29 ladder-logic (PLC) symbols such as coils, timers and comparison blocks. They appear as their own category in the icon picker. No settings.

### Hello JS

A tiny JavaScript example: a counter variable, a settings page and one action. It is the starting point for the [plugin tutorials](https://deccoyi.github.io/macro-grid-plugin/tutorials/js-hello-world).

## Missing plugin in an imported profile

Importing a profile that uses a plugin you do not have tells you which one. Install it and the buttons start working.
