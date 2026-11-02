# Voicemod: get your API key and connect Macro Grid

Change your voice, switch voices and mute from your phone. The Voicemod plugin talks to the Voicemod app on your PC through its Control API. Voicemod gives that API only to people who ask for a key, so **you need your own key**. Macro Grid cannot include one.

**You need:** Windows, the Voicemod app, a Voicemod Control API key (free), and the Voicemod plugin in Macro Grid.

::: info Not affiliated
Macro Grid is an independent project. It is not affiliated with, endorsed by or sponsored by Voicemod. Voicemod is a trademark of its owner.
:::

## 1. Install and start Voicemod {#install-voicemod}

Voicemod's Control API works on **Windows only**. Install the Voicemod app, start it and leave it running. The plugin cannot connect while the app is closed.

## 2. Request your API key {#request-key}

1. Open the [Voicemod Control API request form](https://voicemod.typeform.com/to/Zh5ZHRED).
2. Fill it in with your own details. When it asks what you plan to build, say that you want to control Voicemod from your phone with Macro Grid, for personal use.
3. Read and accept the [Voicemod Control API Terms](https://docs.google.com/document/d/11KFv4jpeV4ALN5omHVSPepiSTwYNXsoXSAxNhkto_XY/edit). Your use of the key is covered by those terms, not by Macro Grid.
4. Send the form. Access is free.

::: warning The key is personal
Do not share your key, post it online or put it in a profile you give to others. If it leaks, ask Voicemod for a new one.
:::

## 3. Wait for the e-mail {#wait-for-email}

Voicemod sends the key by e-mail. It is not instant. Check your spam folder if nothing arrives after a few days.

## 4. Enter the key in Macro Grid {#enter-key}

1. Install the Voicemod plugin (**Plugins → Discover**) if you have not yet.
2. In the editor open **Plugins** and click the gear next to Voicemod.
3. Turn **Enabled** on.
4. Paste your key into the API key field and **Save**.

## 5. Check the connection {#check-connection}

Start Voicemod first. A few moments after you save, the plugin status shows that it is connected. Switch **Status bar** on for Voicemod in the Plugins window to see it in the editor's status bar. If it does not connect, see the next section.

## Troubleshooting {#troubleshooting}

**Not connected, Voicemod is not running.** Start the Voicemod app. The plugin keeps trying and connects when the app is up.

**Wrong key (error 401).** Voicemod refused the key. Paste it again from the e-mail and check that nothing extra was copied: no spaces and no line breaks. If it still fails, ask Voicemod about your key.

**Port blocked.** The Voicemod app listens on this PC only, on port 59129 first and then on some fallback ports. A firewall or security program that blocks local connections can stop it. Allow local connections for Macro Grid and Voicemod, then restart both.

## Privacy {#privacy}

- The plugin talks only to the Voicemod app on this PC. It does not send your key or anything else to a server on the internet.
- Your key is stored encrypted for the current Windows user. Another Windows user on the same PC cannot read it.
