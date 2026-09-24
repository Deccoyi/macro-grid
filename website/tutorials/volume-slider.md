# A volume slider

Build a slider that sets the Windows master volume and follows it when the volume changes elsewhere.

**You need:** a paired device (see [Pair a device](/guide/pairing)).

1. In the editor click **Add widget** and choose **Slider**. Resize it so it is wide (a full row works well).
2. In the inspector set **Min** to `0`, **Max** to `100` and **Step** to `1`. Give it a caption such as `Volume`.
3. Under **Variable driving its position** pick `system.audio.master`. The slider now shows the current volume and moves when it changes.
4. Under **Actions**, on **Value changed**, click **+ Add action** and choose **Master volume**. It applies the slider's value.
5. Click **Save**.

Drag the slider on your phone: the Windows volume follows when you release. Change the volume with your keyboard's volume keys and the slider moves.

## Add a mute button

1. Add a **Toggle** with the text `Mute`.
2. On **Turns on** add **Mute/unmute** with **Mute**. On **Turns off** add the same action with **Unmute**.
3. To keep the toggle honest when you mute from elsewhere, use a [dynamic rule](/guide/dynamic) on its background: if `system.audio.muted` **equal** `true`, red.

::: tip
Instead of a toggle you can use a plain **Button** with the **Toggle mute** action, and a dynamic text such as `{system.audio.muted|MUTED/LIVE}`.
:::
