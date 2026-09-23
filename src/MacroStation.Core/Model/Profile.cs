using System.Text.Json.Nodes;

namespace MacroStation.Core.Model;

public sealed class Profile
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; } = "Profile";
    public List<Page> Pages { get; set; } = [];

    /// <summary>Which "Önizleme" device preset (or preview profile id) the editor should switch to when
    /// this profile is opened — null/empty means "Serbest" (free). Purely an editor convenience; the
    /// server/client never read it. If the referenced preset no longer exists (a custom preview profile
    /// was deleted in Tercihler) the editor falls back to free on its own — this field is left as-is.</summary>
    public string? PreviewDeviceId { get; set; }

    /// <summary>Foreground-window rules that auto-switch an opted-in device to this profile — see
    /// docs/auto-profile-switch.md. Empty means this profile never triggers an automatic switch.</summary>
    public List<AppMatch> AppMatches { get; set; } = [];

    public Page? FindPage(string pageId) => Pages.FirstOrDefault(p => p.Id == pageId);

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];
}

/// <summary>One "switch to this profile when this app is in the foreground" rule (docs/auto-profile-switch.md).</summary>
public sealed class AppMatch
{
    /// <summary>Executable name, e.g. "Player.exe" — matched case-insensitively against the foreground
    /// window's owning process.</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>Optional extra filter: the window title must contain this (case-insensitive) too. Null/empty
    /// means any window of that process matches.</summary>
    public string? TitleContains { get; set; }
}

public sealed class Page
{
    public string Id { get; set; } = Profile.NewId();
    public string Name { get; set; } = "Page";
    public int Cols { get; set; } = 4;
    public int Rows { get; set; } = 3;
    /// <summary>Gap between grid cells, in CSS px. Matches the renderer's own default so old profiles without this field still look the same.</summary>
    public int Gap { get; set; } = 10;
    /// <summary>Padding around the grid, in CSS px.</summary>
    public int Padding { get; set; } = 0;
    /// <summary>How the grid is placed within the page when it doesn't fill the available space: "start" | "center" | "end".</summary>
    public string Alignment { get; set; } = "center";
    public List<Widget> Widgets { get; set; } = [];

    public Widget? FindWidget(string widgetId) => Widgets.FirstOrDefault(w => w.Id == widgetId);
}

public sealed class Widget
{
    public string Id { get; set; } = Profile.NewId();

    /// <summary>See <see cref="WidgetTypes"/>.</summary>
    public string Type { get; set; } = WidgetTypes.Button;

    // Grid placement: 0-based cell and span in cells.
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; } = 1;
    public int H { get; set; } = 1;

    /// <summary>Display text; may contain variable templates such as "Live: {obs.stream.duration}".</summary>
    public string? Text { get; set; }

    public WidgetStyle Style { get; set; } = new();

    /// <summary>User CSS; size/position properties are stripped by the renderer's sanitizer.</summary>
    public string? CustomCss { get; set; }

    /// <summary>Type specific settings (slider min/max, web url, ...).</summary>
    public JsonObject? Props { get; set; }

    /// <summary>Event name (see <see cref="WidgetEvents"/>) to actions executed in order.</summary>
    public Dictionary<string, List<ActionBinding>> Actions { get; set; } = [];

    /// <summary>
    /// Property path (e.g. "style.background") to the rule that computes it from a live variable.
    /// A dynamized property still keeps its normal static value in <see cref="Style"/> — the dynamic
    /// result only overrides it once the bound variable's value actually matches a case (or the
    /// binding's own default, if any). See <see cref="DynamicRuleEvaluator"/>.
    /// </summary>
    public Dictionary<string, DynamicBinding> Dynamic { get; set; } = [];
}

/// <summary>
/// One dynamized property: resolves to the first matching case's result (evaluated in order), or
/// <see cref="Default"/> if none match. Deliberately just data — no expression language, no code
/// execution; see <see cref="DynamicRuleEvaluator"/>.
/// </summary>
public sealed class DynamicBinding
{
    public List<DynamicCase> Cases { get; set; } = [];
    public string? Default { get; set; }
}

/// <summary>One "if" branch: Result applies when Condition evaluates to true.</summary>
public sealed record DynamicCase(ConditionNode Condition, string Result);

/// <summary>
/// A boolean condition tree: a single comparison (Kind = "compare") or a combinator over child nodes
/// (Kind = "and" | "or" | "xor" | "not"). This is the entire "logic" a dynamized property can express —
/// there is no way to reference anything beyond a named variable and a comparison, by construction.
/// </summary>
public sealed class ConditionNode
{
    /// <summary>"compare" | "and" | "or" | "xor" | "not"</summary>
    public string Kind { get; set; } = ConditionKinds.Compare;

    // Used when Kind == "compare".
    public string? Variable { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }
    /// <summary>Second bound for <see cref="DynamicOperators.Between"/> only.</summary>
    public string? Value2 { get; set; }

    /// <summary>Used when Kind is "and"/"or"/"xor" (any number of children) or "not" (exactly one).</summary>
    public List<ConditionNode> Children { get; set; } = [];
}

public static class ConditionKinds
{
    public const string Compare = "compare";
    public const string And = "and";
    public const string Or = "or";
    /// <summary>True when an odd number of children are true — the standard generalization of binary XOR to N operands.</summary>
    public const string Xor = "xor";
    public const string Not = "not";
}

public static class DynamicOperators
{
    public const string GreaterThan = ">";
    public const string GreaterOrEqual = ">=";
    public const string LessThan = "<";
    public const string LessOrEqual = "<=";
    public const string Equal = "==";
    public const string NotEqual = "!=";
    /// <summary>Inclusive: min(Value,Value2) &lt;= live &lt;= max(Value,Value2).</summary>
    public const string Between = "between";
}

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

public sealed record ActionBinding(string Type, JsonObject Settings);

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
}

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
    /// (client's SliderContent/KnobContent "onCommit", not every intermediate onChange tick — see
    /// docs/agent-notes.md on why not every tick hits the wire). <see cref="Plugin.Abstractions.ActionContext.Value"/>
    /// carries the dragged value; bindings on this event read it instead of a static setting.</summary>
    public const string ValueChange = "valueChange";
}
