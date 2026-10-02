using MacroGrid.Core.Model;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>
/// A widget's text when it is dynamized: the editor's dynamize rules pick which template applies
/// (the first matching case, else the binding's default, else the widget's own static text), and the
/// chosen template is then rendered like any other text, so a rule's result may itself contain
/// <c>{variables}</c>.
/// </summary>
public static class DynamicText
{
    /// <summary>The <see cref="Widget.Dynamic"/> key the editor stores the text binding under.</summary>
    public const string PropertyKey = "text";

    public static bool IsDynamic(Widget widget) => widget.Dynamic.ContainsKey(PropertyKey);

    /// <summary>The rendered text a client should show right now, or null when the client's own static
    /// text is already right (no variables in it and the widget is not dynamized).</summary>
    public static string? Resolve(Widget widget, IVariableStore variables)
    {
        var dynamic = IsDynamic(widget);
        var template = dynamic ? ChooseTemplate(widget, variables) : widget.Text;
        if (string.IsNullOrEmpty(template)) return dynamic ? "" : null;

        var parsed = Template.Parse(template);
        if (parsed.VariableNames.Count == 0 && !dynamic) return null;
        return parsed.Render(variables);
    }

    /// <summary>Every variable whose change can alter <see cref="Resolve"/>: the rule conditions plus the
    /// variables inside every template that could be chosen.</summary>
    public static HashSet<string> Dependencies(Widget widget)
    {
        var names = new HashSet<string>();
        AddTemplateVariables(widget.Text, names);
        if (widget.Dynamic.TryGetValue(PropertyKey, out var binding))
        {
            AddTemplateVariables(binding.Default, names);
            foreach (var c in binding.Cases)
            {
                AddTemplateVariables(c.Result, names);
                foreach (var v in DynamicRuleEvaluator.CollectVariables(c.Condition)) names.Add(v);
            }
        }
        return names;
    }

    /// <summary>The variables the widget's current text needs and that have no value now, with no placeholder to cover for them
    /// (a placeholder or an "is unavailable" rule means the person already handled it). Empty when the text uses none, or the widget opts out
    /// with <c>props.hideUnavailable</c>.</summary>
    public static IReadOnlyList<string> UnavailableSources(Widget widget, IVariableStore variables)
    {
        if (widget.Props?["hideUnavailable"]?.GetValueKind() == System.Text.Json.JsonValueKind.True) return [];

        var names = new List<string>();
        var template = IsDynamic(widget) ? ChooseTemplate(widget, variables) : widget.Text;
        if (!string.IsNullOrEmpty(template))
            names.AddRange(Template.Parse(template).VariablesWithoutPlaceholder);

        var bound = widget.Type is WidgetTypes.Slider or WidgetTypes.Knob ? widget.Props?["valueVariable"]?.GetValue<string>() : null;
        if (!string.IsNullOrEmpty(bound)) names.Add(bound);

        return [.. names.Where(n => !SelfVariables.IsSelfName(n) && variables.Get(n) is null).Distinct()];
    }

    private static string? ChooseTemplate(Widget widget, IVariableStore variables) =>
        DynamicRuleEvaluator.Evaluate(widget.Dynamic[PropertyKey], variables) ?? widget.Text;

    private static void AddTemplateVariables(string? text, HashSet<string> names)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (var name in Template.Parse(text).VariableNames) names.Add(name);
    }
}
