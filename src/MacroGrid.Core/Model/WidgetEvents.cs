namespace MacroGrid.Core.Model;

public static class WidgetEvents
{
    public const string Press = "press";
    public const string Release = "release";
    public const string LongPress = "longPress";
    public const string DoubleTap = "doubleTap";

    /// <summary>Fired instead of <see cref="Press"/> on a <see cref="WidgetTypes.Toggle"/> widget when it flips on/off.</summary>
    public const string ToggleOn = "toggleOn";
    public const string ToggleOff = "toggleOff";

    /// <summary>Fired on a <see cref="WidgetTypes.Slider"/>/<see cref="WidgetTypes.Knob"/> once a drag ends
    /// (client's SliderContent/KnobContent "onCommit", not every intermediate onChange tick, so the
    /// wire is not flooded while dragging). <see cref="Plugin.Abstractions.ActionContext.Value"/>
    /// carries the dragged value; bindings on this event read it instead of a static setting.</summary>
    public const string ValueChange = "valueChange";
}
