# Editor icon set: drawing brief

Status: **done.** Profile and Page turned out to already have good matches in the icon set the project uses (Lucide: `LibraryBig` for
Profile, `LayoutTemplate` for Page), so nothing custom was needed for those. The five dock guide glyphs were drawn and are final, in
[icons/](icons/) (`dock-left.svg`, `dock-right.svg`, `dock-top.svg`, `dock-bottom.svg`, `dock-center.svg`). Nothing is left to draw.

This file is kept as a record of the brief that was sent out, in case a future icon needs the same process.

## Style used

- SVG, 24 x 24 view box, 2 px empty margin (drawn inside 20 x 20).
- Outline only: stroke width 2, round line caps and joins, corner radius about 2.
- One colour, `currentColor`; the application colours the icon at render time.
- Simple enough to read at 12 px.

## What was asked for

Five small targets that tell the user where a dragged panel will land if dropped there. Each is a window frame (a wide rectangle,
18 x 14 units within the 24 x 24 grid) with the landing area filled and separated from the rest by a line.

| Icon | What it is for | What was drawn |
|---|---|---|
| **Dock left** | Drop here to put the panel on the left side. | Frame with the left third filled, a vertical line marking it off. |
| **Dock right** | Drop here to put the panel on the right side. | Frame with the right third filled, a vertical line marking it off. |
| **Dock top** | Drop here to put the panel at the top. | Frame with the top third filled, a horizontal line marking it off. |
| **Dock bottom** | Drop here to put the panel at the bottom. | Frame with the bottom third filled, a horizontal line marking it off. |
| **Dock as tab** | Drop here to add the panel as a tab to this group, instead of splitting it. | The whole frame filled, plus a tab-strip line near the top with one short divider marking a single tab. |
