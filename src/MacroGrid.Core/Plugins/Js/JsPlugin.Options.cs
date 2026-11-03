using Jint;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

public sealed partial class JsPlugin
{
    // ---- options sources (host.options.source) ----
    // A source answers from the plugin's memory inside the normal call budget. A failure is an answer with an error, not an exception: the
    // editor asks again on every keystroke, so these calls neither count towards switching the plugin off nor add Error List lines, and
    // nothing the script said (labels, error text) is logged.

    /// <summary>How long a request waits for the plugin thread: the script's own budget plus the time to get its turn.</summary>
    private TimeSpan OptionsWait => _limits.CallTimeout + TimeSpan.FromSeconds(1);

    internal async Task<OptionsResult> SearchOptionsAsync(string sourceId, JsonObject currentValues, OptionsQuery query, CancellationToken ct)
    {
        var queryJson = JsonSerializer.Serialize(new { text = query.Text, offset = query.Offset, limit = query.Limit, selected = query.Selected, refresh = query.Refresh });
        var valuesJson = currentValues.ToJsonString();
        try
        {
            var json = await Post(() => InvokeOptionsSource(sourceId, queryJson, valuesJson)).WaitAsync(OptionsWait, ct);
            return JsOptions.Read(json, _manifest.Id);
        }
        catch (TimeoutException) { return JsOptions.Fail("The plugin did not answer in time."); }
        catch (ObjectDisposedException) { return JsOptions.Fail("The plugin is not running."); }
    }

    private string? InvokeOptionsSource(string sourceId, string queryJson, string valuesJson)
    {
        try
        {
            var result = _engine!.Invoke("__optionsSource", sourceId, queryJson, valuesJson);
            return result.IsString() ? result.AsString() : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Only the kind of failure, never its text.
            _logger.LogDebug("[{PluginId}] an options source call failed ({Kind})", _manifest.Id, ex.GetType().Name);
            return JsonSerializer.Serialize(new { error = "The plugin could not answer (it took too long or failed)." });
        }
    }
}
