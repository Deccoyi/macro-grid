using MacroGrid.Core.Model;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Widgets;

/// <summary>Sits in front of the shared store while one widget is evaluated for one device: answers the <c>self.*</c> names from that widget's own
/// state and forwards every other name. An unknown <c>self.*</c> name is unavailable.</summary>
internal sealed class SelfVariableScope(IVariableStore inner, ClientSession session, Widget widget, ToggleStateStore toggles, LastResultStore? results) : IVariableStore
{
    public object? Get(string name)
    {
        if (!SelfVariables.IsSelfName(name)) return inner.Get(name);
        var last = results?.Get(widget.Id) ?? ("", "");
        return name.ToLowerInvariant() switch
        {
            "self.toggled" => toggles.Get(widget.Id),
            "self.busy" => session.Busy.ContainsKey(widget.Id),
            "self.pressed" => session.Pressed.ContainsKey(widget.Id),
            "self.lastresult" => last.Item1,
            "self.lasterror" => last.Item2,
            _ => null,
        };
    }

    public void Set(string name, object? value)
    {
        if (!SelfVariables.IsSelfName(name)) inner.Set(name, value);
    }

    public void Remove(string name)
    {
        if (!SelfVariables.IsSelfName(name)) inner.Remove(name);
    }
}
