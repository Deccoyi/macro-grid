# Auto-switching by app

A device can follow the **active window** on your PC: bring your media player to the front and the phone switches to the player's profile; close it and the phone goes back.

## Set it up

1. **Add rules to a profile.** Select the profile, open **Auto-switch rules**, then **Pick from running apps…** or type an app name such as `Player.exe`, and **Add**.
2. **Turn it on for the device.** In **Pairing**, switch on **Follow active window** for that device. It is opt-in per device.
3. **Save.**

## How it behaves

- The window that is in front decides. When a window with a rule comes to the front, the device switches to that profile.
- When you move to a window **without** a rule, the device returns to the profile you last picked by hand (or the default).
- If several profiles match the same window, the first one wins.
- A window counts as closed when its program has no visible window left. A player hidden in the tray counts as closed.
- A profile you pick by hand becomes the base: "broadcast profile, open player, close player, back to broadcast profile" works naturally.

## Lock

The phone's profile drawer has a **lock** switch. While it is on, automatic switching pauses, but you can still pick profiles by hand. Turning it off applies the current state once.

## Not covered

Full-screen or game detection, matching by regular expression, and per-page switching. The browser deck does not take part.
