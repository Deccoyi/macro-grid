namespace MacroGrid.Core.Model;

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
