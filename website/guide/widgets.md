# Widgets

A widget is one element on a page's grid. Pick the type when you add it.

| Type | Use it for |
|---|---|
| **Button** | Run actions when pressed, released, long pressed or double tapped. |
| **Toggle** | An on/off switch. A press flips it instead of firing Press. |
| **Label** | Display text or live values, no interaction needed. |
| **Image** | Show a picture from an `https://` URL or a `data:image/...` URL, with an optional caption. |
| **Slider** | A drag control with min, max and step. |
| **Knob** | A rotary control with the same settings as a slider. |
| **Web** | Shows a web page, for example a chat, on your deck. It has a small flask icon in the Toolbox. |
| **Plugin** | A widget a plugin draws, such as a gauge. It is listed under the plugin's name. See [Plugins](/guide/plugins). |

## Events

Each widget type can fire different events. You bind [actions](/guide/actions) to them in the **Actions** section.

| Widget | Events |
|---|---|
| Button | Press, Release, Long press, Double tap |
| Toggle | Turns on, Turns off |
| Slider, Knob | Value changed |

::: warning Long press and double tap add to Press
Every touch is also a press, so **Long press** and **Double tap** run *in addition to* Press and Release, not instead of them. If you use them, keep the Press action harmless or leave it empty.
:::

## Text, icons and layout

- **Text** may contain live values: `CPU {system.cpu|0}%`. See [Variables and text](/guide/variables).
- Set horizontal and vertical alignment, the font size, and an **icon** with its size and position (above, below, left or right of the text). The icon takes the colour of the text.
- Add an icon with **Pick icon…** (see [Styling](/guide/styling)).

## Sliders and knobs

Set **Min**, **Max** and **Step**. The bound "Value changed" action runs when a drag ends (on release), for example **Master volume**.

To make the control *show* a value from the PC, pick a **variable driving its position**, such as `system.audio.master`. The position then follows that variable, even when it changes from another device or from Windows itself. With no variable, the position only reflects the last drag on that device.

## Toggles

A toggle has a state (on or off) that the server tracks. Bind actions to **Turns on** and **Turns off**, for example mute and unmute. To keep its look in step with reality (for example a mic that was muted elsewhere), use a [dynamic rule](/guide/dynamic) based on a variable.

## Web widgets

A web widget has a **Keep loaded** switch in its properties. It keeps its page running when you go to another page of the grid, so a chat does not reload each time. A phone runs only a few at once, so the editor warns when a page has more than three web widgets.

## Names

Every widget has a **name**, which you change in **Properties**. Messages and pickers use it, so a faulty widget is easy to find. A name must be unique on its page (letters are compared without case).
