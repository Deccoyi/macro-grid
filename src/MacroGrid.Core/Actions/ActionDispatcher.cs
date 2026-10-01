using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Actions;

/// <summary>Holds every registered action type (built-in and plugin) and runs a widget's bindings for an event.</summary>
public sealed class ActionDispatcher(IEnumerable<IActionHandler> handlers, ILogger<ActionDispatcher> logger, IVariableStore? variables = null)
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
    /// A failing action (an exception, or an outcome of kind Failed) is logged and does not stop the following ones; it is collected so the
    /// caller can surface it too (editor status bar, a toast on the device that pressed the widget) —
    /// a stale binding should be visibly wrong, never a silent no-op.
    /// </summary>
    public async Task<IReadOnlyList<ActionFailure>> DispatchAsync(Widget widget, string eventName, ActionContext context, CancellationToken cancellationToken)
    {
        List<ActionFailure>? errors = null;

        // A press handler that implements IReleaseAwareAction (e.g. "play while held") learns about the
        // release here, before this event's own bindings run — same widget, same press-time settings.
        if (eventName == WidgetEvents.Release && widget.Actions.TryGetValue(WidgetEvents.Press, out var pressBindings))
        {
            foreach (var binding in pressBindings)
            {
                if (!_handlers.TryGetValue(binding.Type, out var handler) || handler is not IReleaseAwareAction releaseAware)
                    continue;

                try
                {
                    await releaseAware.ReleaseAsync(context, ResolveVariables(handler, binding.Settings), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Release of action {Type} failed on widget {WidgetId}", binding.Type, widget.Id);
                    (errors ??= []).Add(Failure(binding.Type, ActionFailureCode.ProviderError, ex.Message));
                }
            }
        }

        if (!widget.Actions.TryGetValue(eventName, out var bindings) || bindings.Count == 0)
            return (IReadOnlyList<ActionFailure>?)errors ?? [];

        var flow = new ActionFlow.Run();
        foreach (var binding in bindings)
        {
            var step = flow.Next(binding.Type, () => IfMet(binding, flow));
            if (step == FlowStep.Stop) break;
            if (step == FlowStep.Skip) continue;

            if (!_handlers.TryGetValue(binding.Type, out var handler))
            {
                logger.LogWarning("Unknown action type {Type} on widget {WidgetId}", binding.Type, widget.Id);
                var text = $"'{PlainText.Clean(binding.Type, 60)}' is not available. Its plugin may be removed or switched off.";
                (errors ??= []).Add(Failure(binding.Type, ActionFailureCode.NotFound, text) with { Missing = true });
                continue;
            }

            var failure = await RunOneAsync(handler, ResolveVariables(handler, binding.Settings), context, widget.Id, cancellationToken);
            flow.Ran(failure is not null);
            if (failure is not null) (errors ??= []).Add(failure);
        }
        return (IReadOnlyList<ActionFailure>?)errors ?? [];
    }

    /// <summary>Whether an If step is met. A missing or unreadable condition, or no variable store, counts as not met.</summary>
    private bool IfMet(ActionBinding binding, ActionFlow.Run flow)
    {
        var when = binding.Settings["when"] is JsonValue w && w.TryGetValue<string>(out var text) ? text : ActionFlow.WhenCondition;
        if (when == ActionFlow.WhenPreviousFailed) return flow.PreviousFailed;
        if (when == ActionFlow.WhenPreviousOk) return !flow.PreviousFailed;
        if (variables is null || binding.Settings["condition"] is not JsonObject condition) return false;
        try
        {
            var node = condition.Deserialize<ConditionNode>(MacroGrid.Protocol.ProtocolJson.Options);
            return node is not null && DynamicRuleEvaluator.Matches(node, variables);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogDebug(ex, "An If step has a condition that cannot be read");
            return false;
        }
    }

    /// <summary>True when an action of this type is registered.</summary>
    public bool Has(string type) => _handlers.ContainsKey(type);

    /// <summary>Runs one registered action directly with the given settings (a plugin widget's <c>macroGrid.run</c>), not through a widget's bindings.
    /// Returns null when it ran, otherwise the failure: unknown type or the action's own error.</summary>
    public async Task<ActionFailure?> RunAsync(string type, JsonObject settings, ActionContext context, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(type, out var handler)) return Failure(type, ActionFailureCode.NotFound, $"Unknown action '{type}'");
        return await RunOneAsync(handler, ResolveVariables(handler, settings), context, null, cancellationToken);
    }

    /// <summary>Runs one handler. A handler with <see cref="IActionOutcomeHandler"/> is asked only for its outcome (never also for
    /// <c>ExecuteAsync</c>); an older handler succeeds unless it throws. A reported failure is a warning in the log, not an error with a stack.</summary>
    private async Task<ActionFailure?> RunOneAsync(IActionHandler handler, JsonObject settings, ActionContext context, string? widgetId, CancellationToken cancellationToken)
    {
        try
        {
            if (handler is not IActionOutcomeHandler withOutcome)
            {
                await handler.ExecuteAsync(context, settings, cancellationToken);
                return null;
            }

            var outcome = await withOutcome.ExecuteWithOutcomeAsync(context, settings, cancellationToken);
            switch (outcome?.Kind)
            {
                case ActionOutcomeKind.Success:
                    return null;
                case ActionOutcomeKind.Accepted:
                    logger.LogDebug("Action {Type} was accepted: {Message}", handler.Type, outcome.Message);
                    return null;
                case ActionOutcomeKind.Failed:
                    var code = outcome.Code ?? ActionFailureCode.ProviderError;
                    logger.LogWarning("Action {Type} on widget {WidgetId} reported {Code}: {Message}", handler.Type, widgetId, code, outcome.Message);
                    return Failure(handler.Type, code, outcome.Message);
                default:
                    logger.LogWarning("Action {Type} returned no outcome", handler.Type);
                    return Failure(handler.Type, ActionFailureCode.ProviderError, null);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Action {Type} failed on widget {WidgetId}", handler.Type, widgetId);
            return Failure(handler.Type, ActionFailureCode.ProviderError, ex.Message);
        }
    }

    /// <summary>Longest failure text kept (it is shown on another device).</summary>
    private const int MaxFailureMessage = 200;

    private static ActionFailure Failure(string type, ActionFailureCode code, string? message)
    {
        var clean = PlainText.Clean(message, MaxFailureMessage);
        return new ActionFailure(type, code, clean.Length > 0 ? clean : DefaultText(code));
    }

    /// <summary>The host has no translation table of its own, so these stay English like the pairing texts.</summary>
    private static string DefaultText(ActionFailureCode code) => code switch
    {
        ActionFailureCode.NotConfigured => "This action is not set up yet.",
        ActionFailureCode.NotConnected => "Not connected.",
        ActionFailureCode.PermissionDenied => "Not allowed.",
        ActionFailureCode.ProviderRejected => "The other side refused the request.",
        ActionFailureCode.InvalidParameter => "A setting of this action is not valid.",
        ActionFailureCode.NotFound => "The target was not found.",
        ActionFailureCode.Timeout => "No answer in time.",
        ActionFailureCode.Unavailable => "Not available right now.",
        _ => "The other side reported an error.",
    };

    /// <summary>
    /// For every field the handler declares with <see cref="SettingField.AllowVariables"/>, replaces the
    /// <c>{variable}</c> templates in the user's text with the current values before the handler runs. The
    /// binding's own settings are never modified (they belong to the saved profile); a copy is returned only
    /// when there is something to resolve.
    /// </summary>
    private JsonObject ResolveVariables(IActionHandler handler, JsonObject settings)
    {
        if (variables is null || handler is not IActionDescriptor descriptor) return settings;

        JsonObject? resolved = null;
        foreach (var field in descriptor.Fields.Where(f => f.AllowVariables))
        {
            if (settings[field.Key] is not JsonValue value || !value.TryGetValue<string>(out var text) || !text.Contains('{')) continue;
            resolved ??= (JsonObject)settings.DeepClone();
            resolved[field.Key] = Template.Parse(text).Render(variables);
        }
        return resolved ?? settings;
    }
}
