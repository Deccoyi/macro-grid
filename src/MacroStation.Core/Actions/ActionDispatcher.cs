using System.Collections.Concurrent;
using MacroStation.Core.Model;
using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Actions;

/// <summary>Holds every registered action type (built-in and plugin) and runs a widget's bindings for an event.</summary>
public sealed class ActionDispatcher(IEnumerable<IActionHandler> handlers, ILogger<ActionDispatcher> logger)
{
    private readonly ConcurrentDictionary<string, IActionHandler> _handlers =
        new(handlers.Select(h => KeyValuePair.Create(h.Type, h)), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IActionHandler> Handlers => [.. _handlers.Values];

    /// <summary>Adds a handler at runtime (a hot-loaded plugin's action). Returns false, changing nothing,
    /// if that type id is already taken.</summary>
    public bool Register(IActionHandler handler) => _handlers.TryAdd(handler.Type, handler);

    /// <summary>Removes a handler only if it is still the given instance, so unloading one plugin can never
    /// drop a same-named handler that belongs to something else.</summary>
    public void Unregister(IActionHandler handler) =>
        _handlers.TryRemove(KeyValuePair.Create(handler.Type, handler));

    /// <summary>
    /// Runs the actions bound to <paramref name="eventName"/> sequentially.
    /// A failing action is logged and does not stop the following ones; its message is collected so the
    /// caller can surface it too (editor status bar, a toast on the device that pressed the widget) —
    /// a stale binding should be visibly wrong, never a silent no-op.
    /// </summary>
    public async Task<IReadOnlyList<string>> DispatchAsync(Widget widget, string eventName, ActionContext context, CancellationToken cancellationToken)
    {
        if (!widget.Actions.TryGetValue(eventName, out var bindings) || bindings.Count == 0)
            return [];

        List<string>? errors = null;
        foreach (var binding in bindings)
        {
            if (!_handlers.TryGetValue(binding.Type, out var handler))
            {
                logger.LogWarning("Unknown action type {Type} on widget {WidgetId}", binding.Type, widget.Id);
                continue;
            }

            try
            {
                await handler.ExecuteAsync(context, binding.Settings, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Action {Type} failed on widget {WidgetId}", binding.Type, widget.Id);
                (errors ??= []).Add(ex.Message);
            }
        }
        return (IReadOnlyList<string>?)errors ?? [];
    }
}
