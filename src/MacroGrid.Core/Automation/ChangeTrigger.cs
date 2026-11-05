using System.Globalization;
using System.Text;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Automation;

/// <summary>The old and the new value that started a <c>variableChange</c> rule, as the text a step can read.</summary>
public sealed record ChangeValues(string Old, string New);

/// <summary>Judges one change of a <c>variableChange</c> trigger and turns values into token text.</summary>
public static class ChangeTrigger
{
    /// <summary>The most characters of a value a step receives. A plugin variable can carry text from the internet.</summary>
    public const int MaxTokenLength = 256;

    /// <summary>The value as text: booleans as true/false, numbers with the invariant culture, control characters removed, at most <see cref="MaxTokenLength"/> characters.</summary>
    public static string Describe(object? value)
    {
        var text = value switch
        {
            null => "",
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
        var sb = new StringBuilder(Math.Min(text.Length, MaxTokenLength));
        foreach (var c in text)
        {
            if (char.IsControl(c)) continue;
            if (sb.Length >= MaxTokenLength) break;
            sb.Append(c);
        }
        // Do not cut a surrogate pair in half.
        if (sb.Length > 0 && char.IsHighSurrogate(sb[^1])) sb.Length--;
        return sb.ToString();
    }

    /// <summary>The same value: equal, or numbers of different types that are equal (1 and 1.0).</summary>
    public static bool SameValue(object? a, object? b) =>
        Equals(a, b) || a is not (null or bool or string or char) && b is not (null or bool or string or char) && TryNumber(a, out var x) && TryNumber(b, out var y) && x == y;

    /// <summary>True when going from <paramref name="old"/> to <paramref name="now"/> (already known to differ) is a start for this trigger.</summary>
    public static bool Matches(AutomationTrigger trigger, object? old, object? now)
    {
        var from = Describe(old);
        var to = Describe(now);
        if (trigger.Mode == AutomationChangeModes.Contains)
            return trigger.To is { Length: > 0 } word && to.Contains(word, StringComparison.OrdinalIgnoreCase) && (trigger.From is null || SameText(from, trigger.From));
        if (trigger.From is not null && !SameText(from, trigger.From)) return false;
        if (trigger.To is not null && !SameText(to, trigger.To)) return false;
        if (trigger.Mode != AutomationChangeModes.Crosses) return true;
        if (trigger.Threshold is not { } limit || !TryNumber(old, out var a) || !TryNumber(now, out var b)) return false;
        return a < limit != b < limit;
    }

    /// <summary>Equal as numbers when both are numbers ("1" and "1.0"), otherwise as text without regard to case ("True" and "true").</summary>
    private static bool SameText(string a, string b) =>
        double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            ? x == y
            : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool TryNumber(object? value, out double number)
    {
        switch (value)
        {
            case null or bool: number = 0; return false;
            case string s: return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            case IConvertible c:
                try { number = c.ToDouble(CultureInfo.InvariantCulture); return double.IsFinite(number); }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { number = 0; return false; }
            default: number = 0; return false;
        }
    }
}

/// <summary>The variable store a rule's steps read: <c>trigger.old</c> and <c>trigger.new</c> are answered here, everything else comes from the real store. Writes pass through.</summary>
internal sealed class TriggerVariableStore(IVariableStore inner, ChangeValues? change) : IVariableStore
{
    public const string Old = "trigger.old";
    public const string New = "trigger.new";

    public object? Get(string name) =>
        name.Equals(Old, StringComparison.OrdinalIgnoreCase) ? change?.Old
        : name.Equals(New, StringComparison.OrdinalIgnoreCase) ? change?.New
        : inner.Get(name);

    public void Set(string name, object? value) => inner.Set(name, value);
    public void Remove(string name) => inner.Remove(name);
}
