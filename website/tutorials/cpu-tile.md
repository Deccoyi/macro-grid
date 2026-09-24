# A live CPU tile

A tile that shows the CPU load and turns amber, then red and blinking, when it gets busy. It teaches [variables](/guide/variables) and [dynamic rules](/guide/dynamic).

1. Add a **Button** (or a **Label** if you do not need it to be pressable).
2. Set its **Text** to `CPU {system.cpu|0}%`. To insert the variable, click **+ Add variable** and pick `system.cpu`. The `|0` shows whole numbers.
3. Set a dark **Background** and a light **Text** color.
4. Click the **lightning-bolt** next to **Background**. In the **Build logic** window:
   - **If** `system.cpu` **greater than** `80` **Then** a red.
   - Click **Add condition** for **Else if** `system.cpu` **greater than** `50` **Then** amber.
   - Leave **Else** unchanged so your dark background shows. **Apply**.
5. Click the lightning-bolt next to **Animation**: **If** `system.cpu` **greater than** `80` **Then** **Blink**. **Apply**.
6. Optional: bind **Press** to **Open application** and choose Task Manager (`taskmgr.exe`).
7. **Save**.

Start something heavy on the PC and watch the tile change on your phone.

## Ideas

- `RAM {system.ram|0}% ({system.ram.used|0.0}/{system.ram.total|0} GB)`
- `{system.time|HH:mm}` as a clock label.
