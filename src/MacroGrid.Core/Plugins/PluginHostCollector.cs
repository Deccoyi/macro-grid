using System.Text.Json.Nodes;
using MacroGrid.Core.Diagnostics;
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
internal sealed class PluginHostCollector(string serverVersion, string dataDirectory, string pluginId, PluginStatusRegistry statusRegistry, ILogger logger, ISecretProtector? secretProtector = null, PluginWidgetEventHub? widgetEvents = null, string pluginName = "", ProblemList? problems = null) : IPluginHost
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

    /// <summary>Turns a plugin's reports into Error List lines under the plugin's name. A plugin may keep <see cref="MaxLines"/> different lines;
    /// a line past that is dropped and logged once, so a plugin that reports something new every second cannot push everything else out of the list.</summary>
    private sealed class DiagnosticsAdapter(ProblemList? list, string pluginId, string pluginName, ILogger logger) : IPluginDiagnostics
    {
        private const int MaxLines = 20;
        private const int MaxKeyLength = 64;
        private bool _loggedDrop;

        public void Report(PluginDiagnosticLevel level, string message, string? key = null)
        {
            if (list is null || string.IsNullOrWhiteSpace(message)) return;
            if (key is { Length: > MaxKeyLength }) key = key[..MaxKeyLength];
            var severity = level switch
            {
                PluginDiagnosticLevel.Error => ProblemSeverity.Error,
                PluginDiagnosticLevel.Warning => ProblemSeverity.Warning,
                _ => ProblemSeverity.Info,
            };
            if (!list.Report(pluginId, pluginName, severity, ProblemCodes.PluginReported, message, key, MaxLines) && !_loggedDrop)
            {
                _loggedDrop = true;
                logger.LogWarning("[{PluginId}] More than {Max} different problems were reported; new ones are dropped until some are resolved", pluginId, MaxLines);
            }
        }

        public void Resolve(string key)
        {
            if (!string.IsNullOrEmpty(key)) list?.ResolveKey(pluginId, key);
        }
    }

    public string ServerVersion { get; } = serverVersion;
    public string SdkVersion { get; } = PluginSdk.Version;
    public string DataDirectory { get; } = dataDirectory;
    public IPluginSecrets Secrets { get; } = new SecretsAdapter(secretProtector, pluginId);
    public IPluginWidgets Widgets { get; } = new WidgetsAdapter(widgetEvents, pluginId, pluginName);
    public IPluginDiagnostics Diagnostics { get; } = new DiagnosticsAdapter(problems, pluginId, pluginName, logger);

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
