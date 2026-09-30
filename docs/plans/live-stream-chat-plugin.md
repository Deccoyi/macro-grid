# Live-stream chat plugin

> Status: **planned, not built; no platform is chosen.** **Repositories:** `macro-grid-plugin` (the plugin), `macro-grid` (nothing new expected: custom plugin widgets and plugin-to-widget events exist).

One plugin per streaming platform, each a C# plugin (only official, signed C# plugins load; a JavaScript plugin cannot hold a WebSocket or listen), all
drawing the same chat widget.

## Parts

1. **Actions only (ship first).** Ad break, switch the stream category between saved favorites, chat modes, where the platform's API offers them. These
   are plain web requests when a button is pressed, no server of ours, no live connection.
2. **Chat widget.** A plugin widget (Web Worker, canvas only) that draws messages the plugin pushes with `host.Widgets.Post`. A widget has no network, so
   emote images can only be drawn from bundled assets; text-only first, a bundled subset of emotes later.
3. **Per-chatter menu.** Ban or a few preset timeouts: the widget sends a request to its plugin (`IPluginWidgetHandler`), the plugin calls the platform.

## Decisions per platform (before any code)

- **How chat arrives without a server of our own.** A platform with a WebSocket event stream: the plugin connects to it, nothing else needed. A platform
  that only pushes events to a public web address cannot be reached by an offline app: it needs a small relay we run (a cost and a maintenance duty) or an
  unofficial route (not acceptable for an official plugin). If a platform needs a relay, it waits.
- **How the account is linked without shipping a secret.** Preferred: a device-code sign-in with no client secret. A platform that needs a secret on every
  token exchange needs each person to register their own app, or a relay. No secret is ever bundled in the plugin. Tokens go through `IPluginSecrets`.
- **Permissions.** C# plugins have no permission gate, so the plugin is reviewed like the other official ones and lists what it connects to in its README.

## Suggested order

Pick one platform that has a WebSocket event stream and a no-secret sign-in; build part 1, then 2, then 3. Another session is already working on a first
platform; this plan is the shared shape, not a second implementation.

## Open questions

1. Which platform first, and does it meet both conditions above?
2. Emotes: text only, or a bundled set?
3. Is a relay we run acceptable for any platform? Recommendation: no.
