---
layout: home
title: Macro Grid
hero:
  name: Macro Grid
  text: A macro deck for your Windows PC, on your phone
  tagline: Design your own buttons, toggles, sliders and knobs. Press keys, open programs, change the volume and control OBS from any phone, tablet or browser on your network, with live values from your PC on screen.
  actions:
    - theme: brand
      text: Quick start
      link: /guide/
    - theme: alt
      text: Tutorials
      link: /tutorials/volume-slider
    - theme: alt
      text: Download
      link: /download
features:
  - icon: { src: /icons/layout-grid.svg }
    title: Design on a grid
    details: Drag widgets in the editor, style them with colors, icons and animation, and check the result in a live preview.
    link: /guide/editor
  - icon: { src: /icons/keyboard.svg }
    title: Actions and macros
    details: Shortcuts, typing text, opening programs and websites, delays, volume, page and profile switching. Several actions on one button make a macro.
    link: /guide/actions
  - icon: { src: /icons/activity.svg }
    title: Live values and rules
    details: Show CPU, RAM, the time or an OBS stream timer. Colors, icons and text change with rules such as "if CPU is above 80, blink red".
    link: /guide/dynamic
  - icon: { src: /icons/sliders-horizontal.svg }
    title: Two-way sliders and knobs
    details: Control the Windows volume and follow it when it changes somewhere else.
    link: /tutorials/volume-slider
  - icon: { src: /icons/app-window.svg }
    title: A profile per app
    details: The deck follows the active window on your PC. Bring your media player forward and the phone shows its profile.
    link: /guide/auto-switch
  - icon: { src: /icons/puzzle.svg }
    title: Plugins
    details: Control OBS, add icon packs, or install plugins made by others. No restart needed.
    link: /guide/plugins
  - icon: { src: /icons/smartphone.svg }
    title: Phone, tablet or browser
    details: An Android app with QR pairing and kiosk mode, or any browser on your network.
    link: /guide/phone-app
  - icon: { src: /icons/package.svg }
    title: Share your decks
    details: Export a profile as one file and import it on another PC.
    link: /guide/profiles
  - icon: { src: /icons/lock.svg }
    title: Stays on your network
    details: No cloud and no account. Devices pair with a PIN and talk to your PC over your own network.
    link: /guide/security
---

<div class="vp-doc" style="max-width: 900px; margin: 0 auto; padding: 24px 24px 64px;">

## How it works

1. Install Macro Grid on your Windows PC. It lives in the notification area.
2. Pair your phone by scanning the QR code in the editor, or open the deck in a browser and enter the PIN.
3. Design your pages in the editor and press **Save**. The phone updates at once.

![The Macro Grid editor](/img/editor-overview.png)

## The ecosystem

- **Macro Grid for Windows**: the server and the editor where you design decks.
- **The phone and tablet app** for Android, or any browser.
- **Plugins**: OBS control, icon packs and more.

[How the parts fit together](/ecosystem)

## Get started

[Download for Windows and Android](/download), then follow the [Quick start](/guide/). Stuck? See [Troubleshooting](/guide/troubleshooting).

::: warning Alpha, AI-generated, at your own risk
All code, design, documentation and artwork of this project, including this site, were created by an AI assistant. Nothing has been reviewed line by line by a human or security-audited. Features and file formats can still change. It is meant for a home or office network you trust, not the internet. Read [Security](/guide/security) first.
:::

</div>
