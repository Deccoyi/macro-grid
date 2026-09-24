# Profiles and pages

A **profile** is a complete deck. It holds one or more **pages**, and each page is a grid of widgets. Use profiles for different situations: "Work", "Streaming", "Media".

## Profiles

In the header, use the **Profile** picker to switch, and the buttons to **rename**, create a **new profile** or **delete** one. Each profile is stored as its own JSON file.

Which profile a device opens:

1. The profile you assigned to that device in [Pairing](/guide/pairing), else
2. the **default profile** in [Preferences](/guide/preferences), else
3. the first profile.

A profile can also be switched by a button (the **Change profile** action), from the drawer on the phone, or automatically by [app rules](/guide/auto-switch).

## Pages

A profile can have many pages. On the phone, swipe left or right to move between them. You can also add a button with the **Change page** action (go to a page, next, previous or back).

In the pages panel you can add, rename, duplicate and delete pages, and **copy a page to another profile**. Selected widgets can be **moved or copied** to another page or profile, too.

## Export and import

- **File → Export Profile (.msprofile)** saves the current profile to a single file.
- **File → Import Profile (.msprofile / .json)…** loads one. If a profile with the same name exists you choose **Overwrite** or **Rename**.
- If the imported profile uses a plugin you do not have, the editor lists which one, so you can install it and the buttons will work.

This is the easiest way to back up a deck or share it with someone.
