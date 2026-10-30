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
internal sealed class PluginHostCollector(string serverVersion, string dataDirectory, string pluginId, PluginStatusRegistry statusRegistry, ILogger logger, ISecretProtector? secretProtector = null, PluginWidgetEventHub? widgetEvents = null, string pluginName = "", ProblemList? problems = null, double diagnosticsCallsPerSecond = 10) : IPluginHost
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

    /// <summary>Turns a plugin's reports into Error List lines under the plugin's name. The source and code are set here, never by the plugin. A plugin
    /// may keep <see cref="MaxLines"/> different lines and make about 10 calls a second; bad input, calls over the rate and lines
    /// over the cap are dropped without an exception, and each kind is logged once for the plugin's lifetime (never the message). After the host is
    /// retired (the plugin unloaded) every call does nothing.</summary>
    private sealed class DiagnosticsAdapter(ProblemList? list, string pluginId, string pluginName, ILogger logger, double callsPerSecond) : IPluginDiagnostics
    {
        private const int MaxLines = 20;
        private const int MaxKeyLength = 64;

        private readonly Lock _lock = new();
        private double _tokens = callsPerSecond;
        private long _lastTick = Environment.TickCount64;
        private bool _retired;
        private bool _loggedCap, _loggedRate, _loggedInput;

        public void Retire()
        {
            lock (_lock) _retired = true;
        }

        public void Report(PluginDiagnosticLevel level, string message, string? key = null)
        {
            if (list is null || !TakeToken()) return;
            if (message is null || string.IsNullOrWhiteSpace(message) || !Enum.IsDefined(level) || (key is not null && !ValidKey(key)))
            {
                LogOnce(ref _loggedInput, "a diagnostics call with a bad level, message or key was dropped");
                return;
            }
            var severity = level switch
            {
                PluginDiagnosticLevel.Error => ProblemSeverity.Error,
                PluginDiagnosticLevel.Warning => ProblemSeverity.Warning,
                _ => ProblemSeverity.Info,
            };
            lock (_lock)
            {
                if (_retired) return;
                if (!list.Report(pluginId, pluginName, severity, ProblemCodes.PluginReported, message, key, MaxLines))
                    LogOnce(ref _loggedCap, $"more than {MaxLines} different problems were reported; new ones are dropped until some are resolved");
            }
        }

        public void Resolve(string key)
        {
            if (list is null || !TakeToken() || !ValidKey(key)) return;
            lock (_lock)
            {
                if (!_retired) list.ResolveKey(pluginId, key);
            }
        }

        public void Clear()
        {
            if (list is null || !TakeToken()) return;
            lock (_lock)
            {
                if (!_retired) list.ClearCode(pluginId, ProblemCodes.PluginReported);
            }
        }

        private static bool ValidKey(string key) =>
            key.Length is > 0 and <= MaxKeyLength && key.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-');

        /// <summary>A token bucket: a burst of 10, then 10 a second.</summary>
        private bool TakeToken()
        {
            lock (_lock)
            {
                var now = Environment.TickCount64;
                _tokens = Math.Min(callsPerSecond, _tokens + (now - _lastTick) / 1000.0 * callsPerSecond);
                _lastTick = now;
                if (_tokens >= 1)
                {
                    _tokens -= 1;
                    return true;
                }
                LogOnce(ref _loggedRate, "diagnostics calls came faster than 10 a second; the extra ones are dropped");
                return false;
            }
        }

        private void LogOnce(ref bool flag, string text)
        {
            if (flag) return;
            flag = true;
            logger.LogWarning("[{PluginId}] {Text}", pluginId, text);
        }
    }

    public string ServerVersion { get; } = serverVersion;
    public string SdkVersion { get; } = PluginSdk.Version;
    public string DataDirectory { get; } = dataDirectory;
    public IPluginSecrets Secrets { get; } = new SecretsAdapter(secretProtector, pluginId);
    public IPluginWidgets Widgets { get; } = new WidgetsAdapter(widgetEvents, pluginId, pluginName);
    private readonly DiagnosticsAdapter _diagnostics = new(problems, pluginId, pluginName, logger, diagnosticsCallsPerSecond);
    public IPluginDiagnostics Diagnostics => _diagnostics;

    /// <summary>Called when the plugin is unloaded: its later calls do nothing, so a late report cannot reappear after the list was cleared.</summary>
    internal void Retire() => _diagnostics.Retire();

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
