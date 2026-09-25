# Engineering guidelines

Short applied checklist for this repository. It only lists rules that are actually followed here; it is a summary, not a copy of any external text. Deviations are recorded in `docs/refactor-notes.md`.

## C# / .NET

Naming and layout
- One public type per file, file name equals type name. Namespaces follow the folder structure.
- File-scoped namespaces. `PascalCase` for types, members and constants; `_camelCase` for private fields; `I` prefix for interfaces; `Async` suffix for methods returning `Task`.
- Keep the default visibility as narrow as possible: `internal` unless another assembly needs the type. The plugin SDK (`MacroGrid.Plugin.Abstractions`) is the only intentionally public contract.
- Long classes are split with `partial` by concern (load / install / permissions) or, for HTTP, by endpoint group; names and namespaces stay the same.
- DTOs are `record`s with `init` properties where they are immutable; wire and on-disk property names are explicit and never derived from a rename.

Nullable and errors
- `Nullable` is enabled everywhere. Public APIs state nullability honestly; no `!` to silence the compiler unless an invariant is commented.
- Validate arguments at public boundaries (`ArgumentNullException.ThrowIfNull`). Catch specific exceptions; a bare `catch` is only allowed at plugin/IO boundaries where failure must not take the host down, and it must log.
- Do not use exceptions for expected control flow.

Async and cancellation
- Async all the way; no `.Result` / `.Wait()` on request or UI paths.
- Every long-running async method takes a `CancellationToken` and passes it on. Background loops observe the host shutdown token.
- Fire-and-forget work is wrapped so exceptions are logged, never unobserved.
- Library code does not capture the UI context unless it must.

Dependency injection and composition
- Register services in one place per concern (extension methods such as `AddXxx`), constructor injection only, no service locator.
- Registration order of `IActionHandler` is behavior (first match wins); keep it explicit and in one helper.
- Long-lived singletons own their resources and implement `IDisposable`/`IAsyncDisposable`; the owner disposes what it creates.

HTTP (minimal APIs)
- Group endpoints by resource with extension methods on `IEndpointRouteBuilder` (`MapProfileApi`, `MapPluginApi`, ...) and `MapGroup`, so `ServerApp` stays a composition root.
- Return typed results (`Results.*`) and use one shared helper for repeated patterns (not found, validation, no-content).
- Keep handlers thin: parse, call a service, map the result. Business logic lives in Core.
- Route names and JSON shapes are contracts: change them only with a note and a matching client change.

Plugin hosting
- Plugins load in their own collectible `AssemblyLoadContext`; shared contract assemblies resolve from the host context so types match across the boundary.
- Plugins are untrusted for stability: every call into a plugin is guarded, timed where needed, and failures are surfaced without crashing the host.
- Permissions are declared in the manifest and enforced by the host, not by the plugin.

WinForms tray host
- One `ApplicationContext` owns the tray icon, menu and windows; the icon is disposed on exit and hidden before shutdown.
- UI-thread work stays short; server, I/O and plugin work runs off the UI thread and marshals back only for UI updates.
- Windows are created lazily and reused; nothing polls when idle. The host must stay light on CPU and memory because it runs next to games and capture software.

Performance habits
- Avoid per-message allocations on hot paths (reuse buffers, `System.Text.Json` source-generated or cached options, no LINQ in tight loops).
- Prefer timers/events over polling; when polling is needed, keep the interval coarse and stop it when nobody listens.

Tests
- Unit tests must not open windows, touch the real registry, or need the network beyond loopback. Prefer real objects over mocks; use temp directories that are cleaned up.

## TypeScript / React / Vite

- `strict` plus `noUncheckedIndexedAccess`, `noUnusedLocals`, `noUnusedParameters`. No `any`; use `unknown` and narrow.
- Export only what another file imports. Barrel files only at package boundaries.
- Components: one component per file, `PascalCase.tsx`; hooks `useXxx.ts`; pure helpers in plain `.ts` next to the feature. Keep components under roughly 200 lines; extract subcomponents and hooks by concern.
- State lives as close as possible to where it is used. Lift only when two siblings need it; use context for cross-cutting values (preferences, theme), not for hot data.
- Effects synchronise with something external and clean up after themselves; derived values are computed during render (or memoised when measured to matter), not stored in state.
- Wire types mirror the protocol exactly and are defined once; UI code never redefines them.
- All user-visible text goes through the i18n dictionaries; both languages keep identical key sets (a test enforces it).
- Browser storage access goes through one small helper that tolerates missing/blocked storage and malformed JSON.
- Native bridge calls are wrapped once behind a typed module; components never call the bridge directly.
- Vite: no environment-specific code in source; assets referenced through imports; build output is never committed.

## Repository hygiene

- English everywhere in code, comments, docs and commits; UI text only in i18n files.
- Conventional Commits, small and grouped by topic.
- No commented-out code; delete it, git remembers. No TODO without a tracked reason.
- Delete code only when it is proven unused (search including reflection, DI, JSON names, routes and other repositories, then build and test). Otherwise list it in `docs/proposals/dead-code-review.md`.
- Behavior-changing ideas go to `docs/proposals/`, not into a cleanup commit.
