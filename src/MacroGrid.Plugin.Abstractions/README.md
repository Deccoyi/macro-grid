# MacroGrid.Plugin.Abstractions

The plugin SDK for [Macro Grid](https://github.com/Deccoyi/macro-grid). It holds the interfaces a C# plugin
implements (`IPlugin`, `IPluginHost`, `IActionHandler`, `IVariableProvider`, `IPluginSettingsPage`,
`IPluginStatusItem`, `IIconPackSource`, ...) and the manifest types. It contains no logic: the Macro Grid server
loads your plugin and supplies its own copy of this assembly at runtime.

## Reference it

```xml
<ItemGroup>
  <PackageReference Include="MacroGrid.Plugin.Abstractions" Version="0.3.0"
                    PrivateAssets="all" ExcludeAssets="runtime" />
</ItemGroup>
```

`ExcludeAssets="runtime"` keeps the SDK dll out of your plugin folder. The server and every plugin must share the
server's copy; a second copy makes `is IActionHandler` checks fail because the same type from two assemblies is two
different types. Never ship your own copy.

## Minimal plugin

The host finds exactly one `IPlugin` implementation in the plugin assembly (parameterless constructor) and calls
`Initialize` once. This is the whole entry point of the PLC Icons plugin:

```csharp
using MacroGrid.Plugin.Abstractions;

public sealed class PlcIconsPlugin : IPlugin
{
    public void Initialize(IPluginHost host) => host.RegisterIconPack(new PlcIconPack());
}
```

Register actions, variable providers, a settings page, status items or icon packs through `host`. Also ship a
`plugin.json` next to the dll; see the plugin authoring guide in the
[plugin repository](https://github.com/Deccoyi/macro-grid-plugin/blob/main/docs/plugin-authoring.md).

## Compatibility

`PluginSdk.Version` is the SDK version. A plugin declares `"sdkVersion": "^0.3.0"` in `plugin.json`; while the SDK is
`0.x`, that matches `0.3.x` only. The package version equals `PluginSdk.Version`, so package `0.3.0` works with
Macro Grid servers whose SDK is `0.3.x`. Breaking rules are in the server repo's `docs/versioning.md`.

Target framework: `net10.0`. License: MIT.

## AI-generated code

This package, like the rest of Macro Grid, was written entirely with AI assistance: the code, the documentation and the
icon. A human directs, reviews and tests the work and is responsible for the releases, but no line was written by hand.
It is provided as is under the MIT license, so review it as you would any third-party dependency before relying on it.
