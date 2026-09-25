# JS plugin runtime

Plugins with `"kind": "js"` in `plugin.json` are a single script that runs in a Jint sandbox inside the server. The
author-facing reference is `macro-grid-plugin/docs/plugin-authoring.md` ("JavaScript plugins"); this note records how
it is built and why.

## Shape

`JsPlugin` (`Core/Plugins/Js/JsPlugin*.cs`) implements `IPlugin`. `PluginManager` creates it instead of loading an assembly
and then treats it exactly like a C# plugin: `Initialize(host)` collects registrations, they are applied to the live
registries, and unload / reload / uninstall / hot loading all work unchanged (`Dispose` stops the thread and the timers).
The script may only register at its top level; `Initialize` returns once the script's first run has finished (10 s cap).

## Threat model and the layers that hold it

A JS plugin is untrusted: the user installs it from a folder and only approves a list of permissions. The layers, in order:

1. **No .NET.** The engine is created without CLR interop, so `System`, `importNamespace` and friends do not exist.
   The only bridge is a set of delegates (`__log`, `__varSet`, ...). A bootstrap script captures them in a closure and deletes
   them from the global scope, then exposes one frozen `host` object built from small wrappers. The script cannot reach the
   delegates, replace `host`, or extend it. (`JsPluginTests` probes all of this.)
2. **Permissions on every call.** `Require(...)` in each host method checks `JsPermissions`. The failure is a
   `JsHostException`, the one CLR exception type the engine converts into a catchable JavaScript `Error`; any other .NET
   exception is a bug and propagates.
3. **Namespaced output.** Variables and action types must start with `<plugin id>.` (also enforced when describing
   variables), so a plugin cannot spoof `system.*` or another plugin.
4. **HTTP is exact.** Only `http:<host>:<port>` pairs that were approved; redirects are off (a redirect could leave the
   approved host); timeout and response size are capped; the call is synchronous on the plugin's own thread.
5. **Budgets per call** (`JsPluginLimits`): time (Jint's timeout constraint), memory, statement count, recursion depth. Each
   entry into the script starts with a fresh budget. Timers cannot be shorter than 100 ms, at most 20, and ticks that pile up
   while the script is busy are dropped.
6. **Isolation.** One thread per plugin, one job at a time. A blocked or slow plugin only delays itself. Five failures in a
   row switch the plugin off (`PluginManager.DisableAsync`, status `Error` with the last message; Reload restarts it).

## Approval

The manifest's `permissions` are validated (`JsPermissions.IsKnown`; an unknown string is an `Error`). If the user has not
approved exactly that set (`PluginPermissionStore`, `plugin-permissions.json` in the data folder) the plugin is not started
and appears as `NeedsApproval` with `PendingPermissions`. The Plugins window lists them in plain language and
`POST /api/plugins/{id}/approve` stores the approval and starts the plugin. An approval covers the set that was shown: an
update that asks for more waits again. Uninstalling forgets the approval.

## Not covered

- `plugin-html`: a plugin drawing its own widget in a sandboxed iframe on the client, talking to the server through a
  `postMessage` bridge. Needs the widget type in both renderers, a client bridge and a server route.
- An async host API (`async`/`await`, promises); scripts are synchronous today, which is why `host.http` blocks the plugin's
  own thread.
- An unresponsive engine call cannot be interrupted from outside; the per-call time limit is what ends it.
