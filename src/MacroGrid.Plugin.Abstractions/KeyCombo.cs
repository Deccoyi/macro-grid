namespace MacroGrid.Plugin.Abstractions;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}

/// <summary>A key chord such as ctrl+shift+s. <see cref="Key"/> is a normalized lower-case key name.</summary>
public sealed record KeyCombo(KeyModifiers Modifiers, string Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("shift");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("alt");
        if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("win");
        parts.Add(Key);
        return string.Join('+', parts);
    }
}
