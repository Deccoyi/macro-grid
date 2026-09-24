
namespace MacroGrid.Core.Model;

public sealed class WidgetStyle
{
    public string? Background { get; set; }
    public string? Foreground { get; set; }
    /// <summary>left | center | right</summary>
    public string? Align { get; set; }
    /// <summary>top | middle | bottom</summary>
    public string? VAlign { get; set; }
    public double? FontSize { get; set; }
    public string? BorderColor { get; set; }
    public double? BorderWidth { get; set; }
    public double? Radius { get; set; }
    public string? Icon { get; set; }
    public double? IconSize { get; set; }
    /// <summary>top | left | right | bottom</summary>
    public string? IconPosition { get; set; }
    /// <summary>Editor-only bookkeeping (which named icon <see cref="Icon"/> was baked from); the server/client never read this, but it must still round-trip or the editor loses it on reload.</summary>
    public string? IconName { get; set; }
    /// <summary>none | blink | pulse. Static default; a dynamic binding on "style.animation" (see WidgetStateService.DynamizableProperties) can override it live per-device.</summary>
    public string? Animation { get; set; }
}
