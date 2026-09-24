# A profile per app

Make your phone show a media-player deck whenever the player is in front, and go back to your normal deck afterwards. Background: [Auto-switching by app](/guide/auto-switch).

1. Create a profile called `Media` (**New profile** in the header) with a few buttons: play/pause, next, previous (use **Shortcut** with the media keys or the player's own hotkeys).
2. With `Media` selected, open **Auto-switch rules** and click **Pick from running apps…**. Start your player first so it is in the list, or type its executable name, for example `Player.exe`, and click **Add**.
3. Open **Pairing** and switch on **Follow active window** for your phone.
4. **Save**.

Now bring the player to the front: the phone switches to `Media`. Click on another window: it returns to your previous profile.

## Tips

- Set a **default profile** in [Preferences](/guide/preferences) for the "nothing matched" case.
- In the phone's drawer, the **lock** switch pauses auto-switching when you want to stay on one profile.
- Picking a profile by hand makes it the base to return to.
