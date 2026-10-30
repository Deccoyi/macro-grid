using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.PluginTool;

public static partial class PluginToolApp
{
    /// <summary>Starts the plugin with the real runtime (the same sandbox, limits and network rules as the app) against a temporary data folder and
    /// shows what it registered. Nothing is installed and nothing is written into the plugin's folder.</summary>
    private static int RunPlugin(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, ["--action", "--settings", "--value", "--seconds", "--data"], ["--deny"], out var folders, out var options, out var problem) || folders.Count != 1)
            return UsageError(problem ?? "run takes one folder", error);

        JsonObject actionSettings = [];
        if (options.TryGetValue("--settings", out var s))
        {
            try { actionSettings = JsonNode.Parse(s[0]) as JsonObject ?? throw new JsonException("not an object"); }
            catch (JsonException ex) { return UsageError($"--settings must be a JSON object: {ex.Message}", error); }
        }
        double? value = null;
        if (options.TryGetValue("--value", out var v))
        {
            if (!double.TryParse(v[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return UsageError("--value must be a number", error);
            value = parsed;
        }
        var seconds = 0.0;
        if (options.TryGetValue("--seconds", out var sec) && (!double.TryParse(sec[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds) || seconds is < 0 or > 3600))
            return UsageError("--seconds must be a number from 0 to 3600", error);

        var folder = folders[0];
        var report = Check(folder, output);
        if (report.HasErrors) return Failed;

        var manifest = report.Manifest!;
        if (manifest.Kind != PluginKind.Js)
        {
            error.WriteLine("error: run starts JavaScript plugins only");
            return Failed;
        }

        var denied = options.TryGetValue("--deny", out var d) ? d : [];
        var permissions = (manifest.Permissions ?? []).Except(denied, StringComparer.OrdinalIgnoreCase).ToArray();

        var ownData = !options.TryGetValue("--data", out var dataOption);
        var dataDir = ownData ? Path.Combine(Path.GetTempPath(), "macrogrid-plugin-" + Guid.NewGuid().ToString("N")) : dataOption![0];
        Directory.CreateDirectory(dataDir);

        var variables = new VariableStore();
        var statuses = new PluginStatusRegistry();
        var notifications = new PluginNotifications();
        notifications.Posted += (title, text) => output.WriteLine($"notice: {title}: {text}");
        var logger = new ToolLogger(output);
        var faulted = new List<string>();

        JsPlugin? plugin = null;
        try
        {
            var entryPath = PluginManager.ResolveEntryPath(folder, manifest.Entry)!;
            plugin = new JsPlugin(manifest, entryPath, new JsPermissions(permissions), variables, null, logger, faulted.Add,
                network: new JsNetworkPolicy([ServerPorts.Default, ServerPorts.Default + 1]), notifications: notifications);
            var host = new PluginHostCollector(PluginSdk.Version, dataDir, manifest.Id, statuses, logger, pluginName: manifest.Name);
            try { plugin.Initialize(host); }
            catch (InvalidOperationException ex)
            {
                error.WriteLine($"error: the plugin did not start: {ex.Message}");
                return Failed;
            }

            output.WriteLine($"permissions: {(permissions.Length == 0 ? "(none)" : string.Join(", ", permissions))}");
            foreach (var action in host.Actions) output.WriteLine($"action: {action.Type} ({action.DisplayName})");
            if (host.SettingsPage is { } page)
                foreach (var field in page.Fields) output.WriteLine($"setting: {field.Key} ({field.Kind})");

            var result = Ok;
            if (options.TryGetValue("--action", out var actionType))
            {
                var handler = host.Actions.FirstOrDefault(a => a.Type == actionType[0]);
                if (handler is null)
                {
                    error.WriteLine($"error: the plugin has no action '{actionType[0]}'");
                    result = Failed;
                }
                else
                {
                    try
                    {
                        handler.ExecuteAsync(new ActionContext("tool", "tool", "tool", null!, value), actionSettings, CancellationToken.None).GetAwaiter().GetResult();
                        output.WriteLine($"ran: {handler.Type}");
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        error.WriteLine($"error: {handler.Type} failed: {ex.Message}");
                        result = Failed;
                    }
                }
            }

            if (seconds > 0) Thread.Sleep(TimeSpan.FromSeconds(seconds));

            foreach (var status in statuses.All) output.WriteLine($"status: {status.Id} [{status.Level}] {status.Text}");
            foreach (var (name, current) in variables.Snapshot().OrderBy(x => x.Key, StringComparer.Ordinal))
                output.WriteLine($"variable: {name} = {JsonSerializer.Serialize(current)}");
            foreach (var reason in faulted) { error.WriteLine($"error: switched off: {reason}"); result = Failed; }
            return result;
        }
        finally
        {
            plugin?.Dispose();
            if (ownData)
            {
                try { Directory.Delete(dataDir, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>The plugin's own log lines and the runtime's warnings, one line each on the tool's output.</summary>
    private sealed class ToolLogger(TextWriter output) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            lock (output) output.WriteLine($"log: {formatter(state, exception)}");
        }
    }
}
