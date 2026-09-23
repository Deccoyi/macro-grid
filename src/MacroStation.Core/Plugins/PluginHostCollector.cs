using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Plugins;

/// <summary>
/// The <see cref="IPluginHost"/> handed to each plugin's Initialize(). Registrations are only collected
/// here — PluginLoader adds the collected instances to the app's own DI container afterwards, the same
/// way built-in actions/providers are registered in ServerApp.cs.
/// </summary>
internal sealed class PluginHostCollector(string serverVersion) : IPluginHost
{
    public string ServerVersion { get; } = serverVersion;
    public string SdkVersion { get; } = PluginSdk.Version;

    public List<IActionHandler> Actions { get; } = [];
    public List<IVariableProvider> VariableProviders { get; } = [];

    public void RegisterAction(IActionHandler handler) => Actions.Add(handler);

    public void RegisterVariableProvider(IVariableProvider provider) => VariableProviders.Add(provider);
}
