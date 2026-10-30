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
4. **The network is exact.** Only `http:<host>:<port>` and `ws:<host>:<port>` pairs that were approved, and the connection itself is
   checked (see "Network rules" below); redirects and the system proxy are off; timeout and response size are capped. `host.http.get/post` block the plugin's own thread until the answer
   arrives; `host.http.getAsync/postAsync` return a promise instead (see "Async requests" below).
5. **Budgets per call** (`JsPluginLimits`): time (Jint's timeout constraint), memory, statement count, recursion depth. Each
   entry into the script starts with a fresh budget. Timers cannot be shorter than 100 ms, at most 20, and ticks that pile up
   while the script is busy are dropped.
6. **Isolation.** One thread per plugin, one job at a time. A blocked or slow plugin only delays itself. Five failures in a
   row switch the plugin off (`PluginManager.DisableAsync`, status `Error` with the last message; Reload restarts it).

## Network rules

Every connection a plugin makes (web request or web socket) goes through `JsNetworkGuard`, from a `SocketsHttpHandler.ConnectCallback` that resolves the
name, drops the addresses the rules refuse and connects to one that was checked, so a second, different answer from the name service cannot be used.

1. **The address must be of the kind the name says.** `JsNetworkGuard.ScopeOf(host)`: `localhost`, `*.localhost` and loopback literals are *local*; private,
   link-local and shared (100.64/10) literals, single-label names and `.local`, `.lan`, `.home.arpa`, `.internal` are *lan*; everything else is *internet*. A local name
   may only connect to a loopback address, a lan name to a private one, an internet name to a public one. The editor labels an approval with the same rules
   (`networkTargetScope`; `tests/shared/network-scope-cases.json` is read by both).
2. **Macro Grid's own ports are never reachable.** On this computer (loopback or one of its own network card addresses) the server's ports (`PluginManager`'s
   `ownPorts`) are refused whatever was approved.
3. **Headers that steer a request or its framing cannot be set:** `Host`, `Origin`, `Connection`, `Upgrade`, `Content-Length`, `Transfer-Encoding`, `TE`, `Trailer`,
   `Expect`, `Sec-*` and `Proxy-*`. A header name or value with a line break or another control character is refused too.

A refusal reaches the script as a catchable error, adds one Error List line (`P102`, counted when it repeats) and one security log line (event 1108). It never
carries the path, a header or a body.

## Storage

Permission `storage`. `host.storage.get(key)` (null when missing), `set(key, value)`, `remove(key)`, `keys()`; a value is anything `JSON.stringify` accepts. The
file is `storage.json` in the plugin's folder (`{ "formatVersion": 1, "values": { ... } }`); the script never names a path. Keys are 1-64 characters of letters,
digits, `.`, `_`, `-`; at most 64 keys, 16 KB per value, 256 KB in total. It is loaded once, kept in memory, written at most every two seconds after a change and
when the plugin stops (through a temporary file), so a crash can lose the last two seconds. It is plain text, like `settings.json`: not a place for secrets. It stays
over an update and goes with uninstall. A reached limit is a catchable error and one `P103` line.

## Notices

Permission `notify`. `host.notify(text)` shows a short tray notice. The title is always the plugin's own name, the text is cleaned (`PlainText.Clean`) and cut to
200 characters, a click does nothing. `PluginNotifications` rate-limits: 3 in a row, then one every 30 seconds per plugin; the rest are dropped with one `P103` line.
With no tray (tests, a shell without one) the call does nothing.

## Web sockets

Permission `ws:<host>:<port>` (one per target, separate from `http:`).

```js
const socket = host.ws.connect('wss://example.com:443/feed', {
  protocols: ['v1'],                       // optional
  headers: { Authorization: 'Bearer ...' }, // optional, the header rules above apply
  onOpen() {}, onMessage(text) {}, onClose(info) {}, // info: { code, reason, error }
});
socket.send('text'); socket.close();
```

`wss://` is allowed for any approved target; plain `ws://` only when the name is *local* or *lan*. Certificates are checked the normal way. `connect` may be called at
any time and throws at once for a missing permission, a refused scheme, a header or a limit; everything later, also a failed connect, arrives through `onClose`
(`error` is set). There is no automatic reconnect; the script uses `host.after`.

The connection and the read loop run on the thread pool and each event is a job on the plugin's thread (`__wsEvent`). The read loop waits for the job before it reads the
next message, so a fast sender is slowed down by the network and nothing is dropped. A job that comes from a socket never has a press window, so a message cannot press
keys. `Dispose` aborts all sockets (reload, permission switched off, fault switch-off, uninstall, revocation).

Limits (`JsPluginLimits`): 2 sockets per plugin; 10 connects a minute; connect timeout 10 s; text messages only, at most 64 KB each way (a larger incoming one closes the
socket and adds a `P103` line); 50 incoming messages a second (the read loop waits, nothing is dropped); 20 outgoing a second with at most 32 waiting (`send` throws);
keep-alive ping every 30 s.

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

## Reporting an action's outcome

A script action can return `{ ok: false, code, message }` to report a failure or `{ ok: 'accepted', message }` when the other side cannot confirm. `code` is
one of the SDK's `ActionFailureCode` names, matched ignoring case (a missing or unknown code is `ProviderError`); `message` must be a string. Any other return value
(nothing, `true`, a number, an object without `ok`) is success, as before. A reported failure is not a script fault: it adds no Error List line and does
not count towards switching the plugin off; a thrown error still does. The returned shape is read as JSON and larger than 4 KB means `ProviderError`.

An `async` action is not waited for unless it is registered with `outcome: true`; then the host waits for the promise (at most
`JsPluginLimits.ActionOutcomeTimeout`, 10 seconds, then `Timeout`), maps the resolved shape the same way and treats a rejection as `ProviderError` with the error's
message. At most 16 such waits can be pending per plugin (one more is `Unavailable` at once) and a dispose settles them. Without `outcome: true` an async action
behaves as before: its rejection is silent.

## Not covered

- A plugin drawing its own widget: done differently, not by the script itself. A plugin ships widget scripts that run in a sandboxed worker on the
  device ([design/plugin-widgets.md](plugin-widgets.md)); the earlier idea of a `plugin-html` iframe was dropped.
- An unresponsive engine call cannot be interrupted from outside; the per-call time limit is what ends it.
