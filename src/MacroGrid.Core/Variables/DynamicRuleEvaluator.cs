using System.Globalization;
using MacroGrid.Core.Model;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>
/// Resolves a <see cref="DynamicBinding"/> against the live variable store. Pure tree-walking over
/// plain data — no expression parsing, no reflection, no access to anything else in the process. That
/// is the entire security boundary for "conditional styling": nothing here can do more than read a
/// named variable and compare it, combined with and/or/xor/not.
/// </summary>
public static class DynamicRuleEvaluator
{
    /// <returns>The first matching case's Result, else Binding.Default, else null (caller keeps the widget's static value).</returns>
    public static string? Evaluate(DynamicBinding binding, IVariableStore variables)
    {
        foreach (var c in binding.Cases)
        {
            if (EvaluateNode(c.Condition, variables)) return c.Result;
        }
        return binding.Default;
    }

    /// <summary>Every variable name referenced anywhere in the tree, so the caller knows which variable changes should trigger a re-evaluation.</summary>
    public static IEnumerable<string> CollectVariables(ConditionNode node)
    {
        if (node.Kind == ConditionKinds.Compare)
        {
            if (node.Variable is not null) yield return node.Variable;
            yield break;
        }
        foreach (var child in node.Children)
            foreach (var v in CollectVariables(child))
                yield return v;
    }

    private static bool EvaluateNode(ConditionNode node, IVariableStore variables) => node.Kind switch
    {
        ConditionKinds.And => node.Children.Count > 0 && node.Children.All(c => EvaluateNode(c, variables)),
        ConditionKinds.Or => node.Children.Any(c => EvaluateNode(c, variables)),
        ConditionKinds.Xor => node.Children.Count(c => EvaluateNode(c, variables)) % 2 == 1,
        ConditionKinds.Not => node.Children.Count == 1 && !EvaluateNode(node.Children[0], variables),
        _ => EvaluateComparison(node, variables),
    };

    private static bool EvaluateComparison(ConditionNode node, IVariableStore variables)
    {
        if (node.Variable is null || node.Operator is null || node.Value is null) return false;
        var liveValue = variables.Get(node.Variable);

        if (node.Operator == DynamicOperators.Between)
        {
            return TryToDouble(liveValue, out var v)
                && double.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                && double.TryParse(node.Value2, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
                && v >= Math.Min(a, b) && v <= Math.Max(a, b);
        }

        // A boolean matches "true"/"false" and "1"/"0" alike (case-insensitive); only == and != mean anything for it.
        if (liveValue is bool actualBool && TryParseBool(node.Value, out var expectedBool))
        {
            return node.Operator switch
            {
                DynamicOperators.Equal => actualBool == expectedBool,
                DynamicOperators.NotEqual => actualBool != expectedBool,
                _ => false,
            };
        }

        if (TryToDouble(liveValue, out var actual) && double.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected))
        {
            return node.Operator switch
            {
                DynamicOperators.GreaterThan => actual > expected,
                DynamicOperators.GreaterOrEqual => actual >= expected,
                DynamicOperators.LessThan => actual < expected,
                DynamicOperators.LessOrEqual => actual <= expected,
                DynamicOperators.Equal => actual == expected,
                DynamicOperators.NotEqual => actual != expected,
                _ => false,
            };
        }

        // Non-numeric (or one side isn't): only equality/inequality make sense, compared as text.
        var actualText = liveValue?.ToString() ?? "";
        return node.Operator switch
        {
            DynamicOperators.Equal => string.Equals(actualText, node.Value, StringComparison.OrdinalIgnoreCase),
            DynamicOperators.NotEqual => !string.Equals(actualText, node.Value, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool TryParseBool(string text, out bool result)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "true" or "1": result = true; return true;
            case "false" or "0": result = false; return true;
            default: result = false; return false;
        }
    }

    private static bool TryToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d: result = d; return true;
            case float f: result = f; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed): result = parsed; return true;
            default: result = 0; return false;
        }
    }
}
