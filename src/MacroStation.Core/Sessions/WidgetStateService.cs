using MacroStation.Core.Model;
using MacroStation.Core.Profiles;
using MacroStation.Core.Variables;
using MacroStation.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Sessions;

/// <summary>
/// Pushes template-driven widget text and toggle state to clients.
/// Live updates are batched at 100ms (max ~10Hz) and only sent when the rendered text actually changed for that client.
/// </summary>
public sealed class WidgetStateService : IHostedService, IDisposable
{
    /// <summary>Widget property paths the editor can dynamize, mapped to the key sent to clients in WidgetStateMessage.Style.</summary>
    private static readonly Dictionary<string, string> DynamizableProperties = new()
    {
        ["style.background"] = "background",
        ["style.foreground"] = "foreground",
        ["style.borderColor"] = "borderColor",
        ["style.animation"] = "animation",
    };

    private readonly VariableStore _variables;
    private readonly SessionRegistry _sessions;
    private readonly ProfileStore _profiles;
    private readonly ToggleStateStore _toggles;
    private readonly ILogger<WidgetStateService> _logger;
    private readonly HashSet<string> _dirtyVariables = [];
    private readonly Lock _dirtyLock = new();
    private Timer? _timer;

    public WidgetStateService(VariableStore variables, SessionRegistry sessions, ProfileStore profiles, ToggleStateStore toggles, ILogger<WidgetStateService> logger)
    {
        _variables = variables;
        _sessions = sessions;
        _profiles = profiles;
        _toggles = toggles;
        _logger = logger;
        _variables.Changed += OnVariableChanged;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => _ = FlushAsync(), null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _variables.Changed -= OnVariableChanged;

    /// <summary>Sends the current rendered text and toggle state of every relevant widget on a page, e.g. right after a layout is shown.</summary>
    public async Task SendInitialAsync(ClientSession session, Page page, CancellationToken ct)
    {
        foreach (var widget in page.Widgets)
        {
            if (!string.IsNullOrEmpty(widget.Text))
            {
                var template = Template.Parse(widget.Text);
                if (template.VariableNames.Count > 0)
                {
                    var text = template.Render(_variables);
                    session.SentTexts[widget.Id] = text;
                    await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Text: text), ct);
                }
            }

            if (widget.Type == WidgetTypes.Toggle)
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Active: _toggles.Get(widget.Id)), ct);

            var style = ResolveDynamicStyle(widget);
            if (style is not null)
            {
                session.SentStyles[widget.Id] = style;
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Style: style), ct);
            }
        }
    }

    /// <returns>Only the properties whose binding currently produced a value (matched a case, or hit the binding's default); null if the widget has no dynamized properties at all.</returns>
    private Dictionary<string, string>? ResolveDynamicStyle(Widget widget)
    {
        if (widget.Dynamic.Count == 0) return null;

        Dictionary<string, string>? resolved = null;
        foreach (var (propertyPath, binding) in widget.Dynamic)
        {
            if (!DynamizableProperties.TryGetValue(propertyPath, out var styleKey)) continue;
            var value = DynamicRuleEvaluator.Evaluate(binding, _variables);
            if (value is null) continue; // no case matched and no default: leave the widget's own static value alone
            resolved ??= [];
            resolved[styleKey] = value;
        }
        return resolved;
    }

    private static bool BindingsDependOn(IEnumerable<DynamicBinding> bindings, HashSet<string> dirtyVariables) =>
        bindings.SelectMany(b => b.Cases).SelectMany(c => DynamicRuleEvaluator.CollectVariables(c.Condition)).Any(dirtyVariables.Contains);

    private static bool StyleChanged(Dictionary<string, string>? previous, Dictionary<string, string> next)
    {
        if (previous is null || previous.Count != next.Count) return true;
        foreach (var (key, value) in next)
        {
            if (!previous.TryGetValue(key, out var prevValue) || prevValue != value) return true;
        }
        return false;
    }

    private void OnVariableChanged(string name)
    {
        lock (_dirtyLock) _dirtyVariables.Add(name);
    }

    private async Task FlushAsync()
    {
        HashSet<string> dirty;
        lock (_dirtyLock)
        {
            if (_dirtyVariables.Count == 0) return;
            dirty = [.. _dirtyVariables];
            _dirtyVariables.Clear();
        }

        foreach (var session in _sessions.All)
        {
            if (!session.IsIdentified || session.ProfileId is null || session.PageId is null) continue;
            var page = _profiles.Get(session.ProfileId)?.FindPage(session.PageId);
            if (page is null) continue;

            foreach (var widget in page.Widgets)
            {
                string? text = null;
                if (!string.IsNullOrEmpty(widget.Text))
                {
                    var template = Template.Parse(widget.Text);
                    if (template.VariableNames.Count > 0 && template.VariableNames.Any(dirty.Contains))
                    {
                        var rendered = template.Render(_variables);
                        if (!session.SentTexts.TryGetValue(widget.Id, out var prevText) || prevText != rendered)
                            text = rendered;
                    }
                }

                Dictionary<string, string>? style = null;
                if (widget.Dynamic.Count > 0 && BindingsDependOn(widget.Dynamic.Values, dirty))
                {
                    var resolved = ResolveDynamicStyle(widget);
                    if (resolved is not null && StyleChanged(session.SentStyles.GetValueOrDefault(widget.Id), resolved))
                        style = resolved;
                }

                if (text is null && style is null) continue;

                if (text is not null) session.SentTexts[widget.Id] = text;
                if (style is not null) session.SentStyles[widget.Id] = style;

                try
                {
                    await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Text: text, Style: style));
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // Client disconnected between the snapshot above and the send; ClientHub will clean up the session.
                    _logger.LogDebug(ex, "Could not push widget.state to {Session}", session.Id);
                }
            }
        }
    }
}
