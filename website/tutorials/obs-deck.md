# A streaming deck with OBS

Switch scenes, go live, record and see the stream timer, all from your phone. This uses the official [OBS plugin](/guide/plugins#obs).

**You need:** OBS Studio 28 or newer, the OBS plugin, and a paired device.

## 1. Enable OBS WebSocket

In OBS open **Tools → WebSocket Server Settings**, tick **Enable WebSocket server**, and set a password if you wish. Note the port (default `4455`).

## 2. Install and connect the plugin

1. Build or download the OBS plugin ([instructions](https://deccoyi.github.io/macro-grid-plugin/guides/obs-plugin)).
2. In the editor: **Plugins → Manage Plugins… → Install from Folder…** and pick the plugin folder (the one with `plugin.json`).
3. Click the gear next to OBS: turn **Enabled** on, set the server (`127.0.0.1` if OBS is on this PC), port and password, **Save**.
4. The status bar shows the connection. Click **Refresh variables**.

## 3. Scene buttons

For each scene add a **Button**. Text: the scene name. On **Press** add the OBS action to change scene and pick the scene from the list (filled from OBS itself).

Make the active scene stand out: add a dynamic **Background** rule, **If** `obs.scene.current` **equal** `Game` **Then** amber.

## 4. Go live

Add a button with the OBS toggle stream action and this text:

```text
{obs.streaming|LIVE/OFFLINE} {obs.stream.duration}
```

Add dynamic rules: **If** `obs.streaming` **equal** `true` **Then** background red and animation **Blink**. Do the same for recording with `obs.recording` and `{obs.record.duration}`.

## 5. Audio

- A **Toggle** with the OBS mute actions for your microphone.
- A **Slider** with **Min** `0`, **Max** `100` and the OBS volume action on **Value changed**. It uses the live dragged value.

## 6. Handy extras

- `Dropped {obs.stream.frames.droppedPercent|0.0}%` and `FPS {obs.stats.fps|0}` as labels.
- A button that shows or hides a scene item, or sets a text source (the text may contain `{variables}`).
- A **Save replay** button if you use the replay buffer.

**Save** and try it. If OBS is closed, the plugin waits and connects as soon as OBS starts.
