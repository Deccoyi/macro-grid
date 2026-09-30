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
   approved host); timeout and response size are capped. `host.http.get/post` block the plugin's own thread until the answer
   arrives; `host.http.getAsync/postAsync` return a promise instead (see "Async requests" below).
5. **Budgets per call** (`JsPluginLimits`): time (Jint's timeout constraint), memory, statement count, recursion depth. Each
   entry into the script starts with a fresh budget. Timers cannot be shorter than 100 ms, at most 20, and ticks that pile up
   while the script is busy are dropped.
6. **Isolation.** One thread per plugin, one job at a time. A blocked or slow plugin only delays itself. Five failures in a
   row switch the plugin off (`PluginManager.DisableAsync`, status `Error` with the last message; Reload restarts it).

## Reporting problems

`host.diagnostics.report(level, message, key?)` (`level` is `'info'`, `'warning'` or `'error'`) puts a line in the editor's Error List under the
plugin's name, and `host.diagnostics.resolve(key)` takes back the lines reported with that key, and `host.diagnostics.clear()` all of them. A report with a key replaces the line of that key. It needs no permission: it only adds a line to
a list the person reads. A message is cut to 300 characters and control, direction-changing and zero-width characters are removed; a key is at most 64 characters of letters, digits, `.`, `_` and `-`; about 10 calls a second are accepted and bad input is dropped without an error; the same message is one line with a count, and a
plugin can keep at most 20 different lines (more are dropped, with one log line). The lines of a plugin are removed when it is reloaded. A C# plugin
uses `IPluginHost.Diagnostics` (`IPluginDiagnostics`) the same way. The status bar item (`host.status`) stays for a one-line state; use a
diagnostic for something the person has to fix.

## Approval

The manifest's `permissions` are validated (`JsPermissions.IsKnown`; an unknown string is an `Error`). If the user has not
approved exactly that set (`PluginPermissionStore`, `plugin-permissions.json` in the data folder) the plugin is not started
and appears as `NeedsApproval` with `PendingPermissions`. The Plugins window lists them in plain language and
`POST /api/plugins/{id}/approve` stores the approval and starts the plugin. An approval covers the set that was shown: an
update that asks for more waits again. Uninstalling forgets the approval. An approved permission can later be switched off on its own
(`PUT /api/plugins/{id}/permissions`, the tick boxes in the Plugins window): the plugin restarts with the permission missing, so the calls that need it
throw the same "not allowed" error, and it stays approved, so switching it back on asks nothing.

## Async requests

`host.http.getAsync(url, options)` and `host.http.postAsync(url, body, options)` return a promise for
`{ status, body }`, so an action can be `async` and `await` a request while the plugin's timers and other actions keep
running. The request itself uses the same rules as the blocking calls (approved `http:<host>:<port>` only, no redirects,
timeout and response-size caps, the `BuildRequest` check is shared), and at most `MaxPendingHttp` (4) requests may be in flight
per plugin; one more rejects immediately. A refused, failed or timed-out request rejects the promise with an `Error`.

How it works: the request runs on the thread pool; when it finishes, the outcome is posted back to the plugin's own thread as one
more job that calls `__httpDone`, which settles the promise, and the engine then drains its promise queue
(`Engine.Advanced.ProcessTasks`). So the script is still never running on two threads at once, and every entry into it keeps its
own time, memory and statement budget. Dispose cancels everything in flight. There is no `fetch`, `setTimeout` or other
browser API; `host.after`/`host.every` remain the timers.

## Not covered

- A plugin drawing its own widget: done differently, not by the script itself. A plugin ships widget scripts that run in a sandboxed worker on the
  device ([design/plugin-widgets.md](plugin-widgets.md)); the earlier idea of a `plugin-html` iframe was dropped.
- An unresponsive engine call cannot be interrupted from outside; the per-call time limit is what ends it.
