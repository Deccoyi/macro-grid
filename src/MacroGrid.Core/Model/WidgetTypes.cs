namespace MacroGrid.Core.Model;

public static class WidgetTypes
{
    public const string Button = "button";
    public const string Toggle = "toggle";
    public const string Slider = "slider";
    public const string Knob = "knob";
    public const string Label = "label";
    public const string Image = "image";
    public const string Web = "web";
    public const string PluginHtml = "plugin-html";

    /// <summary>A custom widget of a plugin: its code runs in a sandboxed worker on the device and draws to a canvas. Props: <c>plugin</c>, <c>widget</c>, <c>settings</c>.</summary>
    public const string PluginWidget = "plugin-widget";
}
