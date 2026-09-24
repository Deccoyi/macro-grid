# The editor

![The Macro Grid editor with a demo deck](/img/editor-overview.png)

The editor opens in the server's own window (from the tray icon). It only works on the PC itself. Other devices on your network cannot open it.

## Layout

- **Menu bar:** **File** (export and import profiles), **Settings** (Preferences), **Plugins** (Manage Plugins), **Help** (version, about and disclaimer, licenses, user agreement).
- **Header:** the profile picker, a device size for the preview, **Preview**, **Refresh variables**, **Pairing**, and **Save**.
- **Pages panel:** the pages of the current profile. Add, rename, duplicate, delete, or copy a page to another profile.
- **Canvas:** the grid. Drag widgets, resize them from the corner, right-click for a menu.
- **Inspector:** properties of the selected widget (or of the page when nothing is selected).
- **Status bar:** server version, connected devices, plugin status and action errors.

## Working on the grid

Select a widget and the inspector on the right shows its appearance, content and actions:

![The inspector for a selected widget](/img/editor-inspector.png)

- Choose **Add widget** and pick a type. It is placed on the first free cell ("No free cell left on this page" means the grid is full; enlarge it in the page properties).
- Drag to move, drag the **Resize** handle to change the size. Widgets snap to the grid and cannot overlap.
- Select several widgets to **duplicate**, **move or copy** them to another page or profile, or delete them.
- Page properties: **Name**, **Grid** (columns and rows), **Gap**, **Padding**, **Alignment** (start, center, end).

## Preview

The device size list (phone or tablet, portrait or landscape, free or custom) changes how the preview canvas looks, so you can check how a page fits your phone. Add your own device sizes in [Preferences](/guide/preferences). Live values are shown in the preview. **Refresh variables** reloads the list of available variables, for example after installing a plugin.

## Saving

Nothing reaches your devices until you click **Save**. Saving sends **only what changed** to connected devices, so the deck does not flash or reset. If you leave with unsaved changes the editor asks first.

## Language and theme

The editor is available in Turkish (default) and English, in dark or light theme: [Preferences](/guide/preferences).
