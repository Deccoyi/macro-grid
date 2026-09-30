using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>
/// An action type that can be bound to a widget event (press, release, longPress, doubleTap).
/// Built-in actions and plugin actions implement the same interface.
/// </summary>
public interface IActionHandler
{
    /// <summary>Unique id, e.g. "core.hotkey" or "demo.switchScene".</summary>
    string Type { get; }

    string DisplayName { get; }

    Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken);
}

/// <summary><paramref name="Value"/> is only set for a <c>valueChange</c> dispatch (a slider/knob drag
/// commit) — the live dragged value, not a static per-binding setting. Every other event leaves it null.</summary>
public sealed record ActionContext(string DeviceId, string PageId, string WidgetId, IDeviceController Device, double? Value = null)
{
    /// <summary>True when a person's touch caused this run (a button press). False for an action a widget's script started without a real touch:
    /// the host then keeps it from typing on the keyboard for a plugin.</summary>
    public bool UserGesture { get; init; } = true;
}
