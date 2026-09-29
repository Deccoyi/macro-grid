using System.Text.Json.Nodes;

namespace MacroGrid.Core.Model;

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

    /// <summary>Optional name, only used to pick this widget in an action (for example the target of <c>core.web</c>). Not required to be unique
    /// (a copied widget keeps it; the editor tells two apart by their page). Actions store the widget's <see cref="Id"/>, so renaming never breaks them.</summary>
    public string? Name { get; set; }

    /// <summary>Display text; may contain variable templates such as "Live: {demo.stream.duration}".</summary>
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
