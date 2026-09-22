namespace MacroStation.Core.Input;

/// <summary>Normalized key names accepted by <see cref="HotkeyParser"/>. Every platform input service must map all of them.</summary>
public static class KnownKeys
{
    public static readonly IReadOnlySet<string> All = Build();

    private static HashSet<string> Build()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var c = 'a'; c <= 'z'; c++) keys.Add(c.ToString());
        for (var d = 0; d <= 9; d++) { keys.Add(d.ToString()); keys.Add($"num{d}"); }
        for (var f = 1; f <= 24; f++) keys.Add($"f{f}");

        string[] named =
        [
            "ctrl", "shift", "alt", "win",
            "enter", "escape", "tab", "space", "backspace", "delete", "insert",
            "home", "end", "pageup", "pagedown", "up", "down", "left", "right",
            "printscreen", "pause", "capslock", "numlock", "scrolllock", "menu",
            "numadd", "numsubtract", "nummultiply", "numdivide", "numdecimal",
            "plus", "minus", "comma", "period", "semicolon", "slash", "backslash",
            "quote", "backquote", "bracketleft", "bracketright", "equal",
            "volumeup", "volumedown", "volumemute",
            "mediaplaypause", "medianext", "mediaprev", "mediastop",
        ];
        keys.UnionWith(named);
        return keys;
    }
}
