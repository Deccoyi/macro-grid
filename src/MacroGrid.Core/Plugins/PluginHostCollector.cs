using System.Text.Json.Nodes;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Security;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Plugins;

/// <summary>
/// The <see cref="IPluginHost"/> handed to each plugin's Initialize(). Registrations are only collected
/// here — PluginLoader adds the collected instances to the app's own DI container afterwards, the same
/// way built-in actions/providers are registered in ServiceRegistration.cs.
/// </summary>
internal sealed class PluginHostCollector(string serverVersion, string dataDirectory, string pluginId, PluginStatusRegistry statusRegistry, ILogger logger, ISecretProtector? secretProtector = null, PluginWidgetEventHub? widgetEvents = null, string pluginName = "") : IPluginHost
{
    /// <summary>Adapts the host's own <see cref="ISecretProtector"/> to the SDK-facing <see cref="IPluginSecrets"/>.
    /// <paramref name="protector"/> is only ever null in a test host that never registered one; a plugin that
    /// calls <see cref="Protect"/> or <see cref="Unprotect"/> there gets a clear failure instead of a value that
    /// looks protected but is not.</summary>
    private sealed class SecretsAdapter(ISecretProtector? protector, string pluginId) : IPluginSecrets
    {
        public string Protect(string secret) => Require().Protect(secret);
        public string? Unprotect(string protectedSecret) => Require().Unprotect(protectedSecret);

        private ISecretProtector Require() => protector
            ?? throw new InvalidOperationException($"Plugin '{pluginId}' called IPluginSecrets, but this host has no secret protector configured.");
    }

    /// <summary>Hands a plugin's events for its widgets to the hub; a host without a hub (some tests) drops them.</summary>
    private sealed class WidgetsAdapter(PluginWidgetEventHub? hub, string pluginId, string pluginName) : IPluginWidgets
    {
        public void Post(string widget, string name, JsonNode? data, string? widgetId = null, string? deviceId = null, bool retain = false) =>
            hub?.Post(pluginName, new PluginWidgetEvent(pluginId, widget, name, data?.DeepClone(), widgetId, deviceId, retain));
    }

    public string ServerVersion { get; } = serverVersion;
    public string SdkVersion { get; } = PluginSdk.Version;
    public string DataDirectory { get; } = dataDirectory;
    public IPluginSecrets Secrets { get; } = new SecretsAdapter(secretProtector, pluginId);
    public IPluginWidgets Widgets { get; } = new WidgetsAdapter(widgetEvents, pluginId, pluginName);

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
