using MacroStation.Core.Model;
using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Actions;

/// <summary>Holds every registered action type (built-in and plugin) and runs a widget's bindings for an event.</summary>
public sealed class ActionDispatcher(IEnumerable<IActionHandler> handlers, ILogger<ActionDispatcher> logger)
{
    private readonly Dictionary<string, IActionHandler> _handlers =
        handlers.ToDictionary(h => h.Type, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IActionHandler> Handlers => _handlers.Values;

    /// <summary>
    /// Runs the actions bound to <paramref name="eventName"/> sequentially.
    /// A failing action is logged and does not stop the following ones.
    /// </summary>
    public async Task DispatchAsync(Widget widget, string eventName, ActionContext context, CancellationToken cancellationToken)
    {
        if (!widget.Actions.TryGetValue(eventName, out var bindings) || bindings.Count == 0)
            return;

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
            }
        }
    }
}
