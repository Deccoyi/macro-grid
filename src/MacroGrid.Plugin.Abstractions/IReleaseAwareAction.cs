using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>Implemented by an <see cref="IActionHandler"/> bound to a widget's <c>press</c> event when it
/// needs to know the button was let go — a generic press/release pairing any action can use, not just a
/// media action (e.g. "play while held", "hold to talk", or any state a widget should only keep while the
/// finger stays down). The host calls <see cref="ReleaseAsync"/>, with the same settings the <c>press</c>
/// binding ran with, when the widget's <c>release</c> event fires — before that event's own bindings run.</summary>
public interface IReleaseAwareAction
{
    Task ReleaseAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken);
}
