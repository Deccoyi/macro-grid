using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Plugins;

/// <summary>
/// The <see cref="IPluginHost"/> handed to each plugin's Initialize(). Registrations are only collected
/// here — PluginLoader adds the collected instances to the app's own DI container afterwards, the same
/// way built-in actions/providers are registered in ServerApp.cs.
/// </summary>
internal sealed class PluginHostCollector(string serverVersion, string dataDirectory, string pluginId, PluginStatusRegistry statusRegistry, ILogger logger) : IPluginHost
{
    public string ServerVersion { get; } = serverVersion;
    public string SdkVersion { get; } = PluginSdk.Version;
    public string DataDirectory { get; } = dataDirectory;

    public List<IActionHandler> Actions { get; } = [];
    public List<IVariableProvider> VariableProviders { get; } = [];
    public List<IIconPackSource> IconPacks { get; } = [];
    public IPluginSettingsPage? SettingsPage { get; private set; }

    public void Log(string message) => logger.LogInformation("[{PluginId}] {Message}", pluginId, message);

    public void RegisterAction(IActionHandler handler) => Actions.Add(handler);

    public void RegisterVariableProvider(IVariableProvider provider) => VariableProviders.Add(provider);

    public void RegisterSettingsPage(IPluginSettingsPage page) => SettingsPage = page;

    public IPluginStatusItem CreateStatusItem(string id) => new PluginStatusItem(statusRegistry, pluginId, id);

    public void RegisterIconPack(IIconPackSource iconPack) => IconPacks.Add(iconPack);
}
