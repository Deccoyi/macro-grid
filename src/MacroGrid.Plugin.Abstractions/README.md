# MacroGrid.Plugin.Abstractions

> **AI notice:** this project is AI-generated and its code has not been reviewed. Macro Grid was built for personal use and
> was later made open source. It is published as is, without any warranty, and you use it entirely at your own risk. We
> accept no responsibility and cannot be held liable for any damage or loss that results from using it (see the MIT license).

The plugin SDK for [Macro Grid](https://github.com/Deccoyi/macro-grid). It holds the interfaces a C# plugin
implements (`IPlugin`, `IPluginHost`, `IActionHandler`, `IVariableProvider`, `IPluginSettingsPage`,
`IPluginStatusItem`, `IIconPackSource`, ...) and the manifest types. It contains no logic: the Macro Grid server
loads your plugin and supplies its own copy of this assembly at runtime.

## Reference it

```xml
<ItemGroup>
  <PackageReference Include="MacroGrid.Plugin.Abstractions" Version="0.4.0"
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

When a provider describes its variables (`IVariableCatalogSource`), give each one a type so the editor can offer the right
value in a condition (for example a true/false choice for a boolean instead of a free text box):

```csharp
new VariableInfo("myplugin.connected", "Whether it is connected", "{myplugin.connected}", "My plugin") { Type = VariableType.Boolean },
new VariableInfo("myplugin.bitrate", "Bitrate", "{myplugin.bitrate|0} kbps", "My plugin") { Type = VariableType.Number, Unit = "kbps" },
new VariableInfo("myplugin.state", "State", "{myplugin.state}", "My plugin") { Values = ["idle", "busy"] },
```

`Type` defaults to `Text`; `Unit` and `Values` are optional.

## Compatibility

`PluginSdk.Version` is the SDK version. A plugin declares `"sdkVersion": "^0.4.0"` in `plugin.json`; while the SDK is
`0.x`, that matches `0.4.x` only. The package version equals `PluginSdk.Version`, so package `0.4.0` works with
Macro Grid servers whose SDK is `0.4.x`. Breaking rules are in the server repo's `docs/guides/versioning.md`.

Target framework: `net10.0`. License: MIT.
