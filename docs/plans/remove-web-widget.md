# Remove the web widget

Status: built on `feat/remove-web-widget`, waiting for the next release. Delete this file once it ships.

## Decision

The owner removed the `web` widget (an embedded page in an iframe on the deck). It was marked experimental, a page can freeze or crash the phone app,
and it needs a security rule set (`WebUrlRule`, an import consent, a crash guard). The `core.web` action ("Change web page") goes with it.

## Rule for old data

A saved profile must still open, nothing is migrated and nothing is deleted behind the person's back.

| Where | What happens to a `web` widget |
|---|---|
| Profile file, import (`.mgprofile`, `.json`), restore from a backup | Loaded and saved unchanged (`WidgetTypes.Web` stays as a legacy value). No error. |
| Editor | Not in the Toolbox. An old widget is drawn as unavailable ("Web widget removed"), the Properties panel explains it, the Error List adds `W213`. Delete works as for any widget. |
| Device (phone app, browser deck) | `LayoutSender` replaces it with a `label` ("Web widget removed", Turkish by the app language) with no `props` and no `actions`, so no client loads a page. |
| `core.web` button step | An unregistered action: the button is marked unavailable, the existing "action missing" paths apply. |

## What was removed

Server: `WebAction`, `WebViewState`, `WebUrlRule` (and the sanitising in save and import), security event 1201, `WidgetStateMessage.Url` and `.Reload`.
Renderer package: `WebContent`, `webUrl.ts`, the `web*` props of `WidgetView`, `WidgetState.url` and `.reload`. Editor: the web fields, the import consent,
the "Change web page" form, the palette entry and their texts.

## Phone app (other repository, not changed here)

Its deck screen imports `isSafeWebUrl`, `webUrlHost` and passes `webUrl`, `webReload`, `webBlocked` and `webBlockedHosts` to `WidgetView`, and keeps
"keep loaded" web guards for `type === "web"`. The server never sends `web` to a device now, so the released app does not crash and loads no page.
When the app moves to the new renderer package it must drop those imports and props (and the crash guard, the "Tap to load" placeholder and the
live-number setting) or the build fails. The app can delete that code at its own pace.

## Not done on purpose

No migration that deletes old widgets or `core.web` steps; no change to `plugin-html` (still a reserved placeholder).
