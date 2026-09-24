# Styling, icons and CSS

## Appearance

In **Appearance** you can set the **Background**, **Text** color, **Border** color and width, corner **Radius**, and an **Animation**: none, **Blink**, **Pulse** or **Pulse (grow and shrink)**. Each color and the animation can be [dynamic](/guide/dynamic).

## Icons

**Pick icon…** opens a searchable picker with the Lucide icon set (ISC license). Plugins can add their own packs; the [PLC Icons](/guide/plugins#plc-icons) plugin adds 29 ladder-logic symbols in its own category. The chosen icon takes the widget's icon color, and you set its size and position relative to the text.

Icons are sent to a device once and cached there.

## Custom CSS

Each widget has a **Custom CSS** box for anything the panel does not cover: gradients, shadows, animations, fonts.

The CSS is sandboxed per widget and sanitized. These are removed, and the editor warns you:

- Anything that changes size or position: `width`, `height`, `position`, `inset`, `top`, `right`, `bottom`, `left`, `margin`, `transform`, `zoom`, `display`.
- `@import`.
- `url()` pointing anywhere other than a `data:` URI.

Gradients, shadows, borders and animations all work.

```css
background: linear-gradient(135deg, #d97706, #b45f04);
box-shadow: 0 4px 14px rgba(0, 0, 0, 0.4);
```

## Theme of the editor

The editor's own light or dark theme never affects your widget colors. Those are always yours.
