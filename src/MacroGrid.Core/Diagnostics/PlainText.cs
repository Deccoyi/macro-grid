namespace MacroGrid.Core.Diagnostics;

/// <summary>Cleans text that a plugin can influence before it is shown to a person (the Error List, a toast on a device).</summary>
public static class PlainText
{
    /// <summary>Removes control characters, bidirectional overrides and zero-width characters (they could reorder or hide text) and cuts the
    /// result to <paramref name="maxLength"/> characters, never between the two halves of a surrogate pair.</summary>
    public static string Clean(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || maxLength <= 0) return "";
        var chars = new List<char>(Math.Min(text.Length, maxLength));
        foreach (var c in text)
        {
            if (char.IsControl(c) || IsInvisibleFormat(c)) continue;
            if (chars.Count >= maxLength) break;
            chars.Add(c);
        }
        if (chars.Count > 0 && char.IsHighSurrogate(chars[^1])) chars.RemoveAt(chars.Count - 1);
        return new string([.. chars]);
    }

    private static bool IsInvisibleFormat(char c) =>
        c is >= '\u200B' and <= '\u200F' or >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069' or '\uFEFF';
}
