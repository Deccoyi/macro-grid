using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

public sealed partial class JsPlugin
{
    private readonly Dictionary<int, TaskCompletionSource<JsonNode?>> _widgetReplies = [];
    private readonly Lock _widgetLock = new();
    private int _nextWidgetRequest;

    /// <summary>Answers a widget's request through the function the script gave to <c>host.widgets.onMessage</c>. The function may return a value or a promise.</summary>
    public async Task<JsonNode?> OnWidgetMessageAsync(PluginWidgetMessage message, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int id;
        lock (_widgetLock)
        {
            id = ++_nextWidgetRequest;
            _widgetReplies[id] = source;
        }

        try
        {
            var json = JsonSerializer.Serialize(new { message.Widget, message.DeviceId, message.PageId, message.WidgetId, message.Settings, message.Data }, WidgetJson);
            _ = Post(() => { Invoke("__widgetMessage", id, json); return 0; }).ContinueWith(t =>
            {
                if (t.IsFaulted) source.TrySetException(t.Exception!.InnerException ?? t.Exception);
            }, TaskScheduler.Default);
            return await source.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            lock (_widgetLock) _widgetReplies.Remove(id);
        }
    }

    private static readonly JsonSerializerOptions WidgetJson = new(JsonSerializerDefaults.Web);

    /// <summary>Called by the script when a widget request settled (from the bootstrap, on the plugin thread).</summary>
    private void WidgetReply(int id, bool ok, string payload)
    {
        TaskCompletionSource<JsonNode?>? source;
        lock (_widgetLock) _widgetReplies.TryGetValue(id, out source);
        if (source is null) return;
        if (ok)
        {
            try { source.TrySetResult(string.IsNullOrEmpty(payload) ? null : JsonNode.Parse(payload)); }
            catch (JsonException ex) { source.TrySetException(ex); }
        }
        else
        {
            source.TrySetException(new InvalidOperationException(Truncate(payload, 300)));
        }
    }

    /// <summary>Sends an event to the plugin's own widgets (<c>host.widgets.post</c>).</summary>
    private void WidgetPost(string json)
    {
        var post = JsonNode.Parse(json)?.AsObject() ?? throw new JsHostException("widgets.post needs a widget and an event name.");
        var widget = post["widget"]?.GetValue<string>();
        var name = post["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(widget) || string.IsNullOrWhiteSpace(name) || name.Length > 64)
            throw new JsHostException("widgets.post needs a widget id and an event name of at most 64 characters.");
        _host!.Widgets.Post(widget, name, post["data"], post["widgetId"]?.GetValue<string>(), post["deviceId"]?.GetValue<string>(), post["retain"]?.GetValue<bool>() ?? false);
    }
}
