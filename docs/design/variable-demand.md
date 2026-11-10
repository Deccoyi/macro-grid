# Variable demand (`IPluginHost.Demand`)

A plugin that polls a slow source (a socket request every second, a process list) cannot tell whether anyone looks at the result. `IPluginHost.Demand`
answers "does anything need this variable right now", so the plugin can skip the work and refresh when the answer turns to yes. Optional: a plugin that
never reads it behaves exactly as before.

## SDK (additive, SDK MINOR)

- `IPluginHost.Demand` is a default member (`NullVariableDemand.Instance`: everything is needed, nothing ever raises), like `Diagnostics` and `Images`.
  It needs no `CompatibilitySuppressions.xml` entry: package validation reports no `CP0006` for a default member. A host that predates it, and a
  plugin built for an older SDK, are unaffected.
- `IVariableDemand.IsObserved(string name)`: true when something needs the variable now.
- `IVariableDemand.IsPrefixObserved(string prefix)`: true when something needs any variable that starts with the prefix (for example `obs.stream.`).
- `IVariableDemand.Changed`: raised when demand may have grown. It is not raised when demand shrinks; ask again at the next tick.

A `false` answer is given only when nothing the host knows of reads the variable. Any doubt (a source that throws) answers `true`.

## What counts as demand (server)

| Reader | How it is counted |
|---|---|
| Widgets on the page each connected device shows | Names from the per-page variable index in `WidgetStateService`: text, dynamic looks, value variable and `{variable}` templates in action settings |
| Automation rules | Variables of the trigger and of the condition |
| Plugin widgets | Variables a widget on a connected device subscribed to |
| A script or plugin reading a variable it did not publish (`host.variables.get`, `IVariableStore.Get`) | A lease of 30 seconds per name, renewed by every read |
| The editor's one-shot live preview (`GET /api/variables/snapshot`) | A lease of 30 seconds on every variable |

`Changed` is raised when a page is shown, a profile is saved, an action handler appears or goes, a rule adds a watched variable, a plugin widget
subscribes, and when a lease starts.

Known limits: the first `get` of an unwatched variable by a script returns the last published value (the lease starts with that read); a plugin that
needs a value on a button press that is not on a shown page (for example a `{variable}` template of an automation action) should keep it fresh itself.

## Using it from a plugin

```csharp
host.Demand.Changed += () => wake.Set();           // hand over to your own loop; keep the handler short

while (!ct.IsCancellationRequested)
{
    if (host.Demand.IsPrefixObserved("obs.stream."))
        PublishStreamStats();                       // skipped while no page, rule or widget needs it
    await wake.WaitAsync(TimeSpan.FromSeconds(1), ct);
}
```

A plugin built against the new SDK that reads `host.Demand` fails on a host that predates it (the member does not exist there), so an official plugin that uses it
sets `minMacroGrid` to the first version that has it.
