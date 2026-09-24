namespace MacroGrid.Windows.Input;

/// <summary>Maps the normalized key names from <c>KnownKeys</c> to Win32 virtual-key codes.</summary>
public static class VirtualKeys
{
    public static readonly IReadOnlyDictionary<string, ushort> Map = Build();

    /// <summary>Keys that must be sent with KEYEVENTF_EXTENDEDKEY, otherwise Windows treats them as numpad keys.</summary>
    public static readonly IReadOnlySet<string> Extended = new HashSet<string>
    {
        "insert", "delete", "home", "end", "pageup", "pagedown",
        "up", "down", "left", "right", "numdivide", "numlock", "printscreen", "menu", "win",
    };

    private static Dictionary<string, ushort> Build()
    {
        var map = new Dictionary<string, ushort>(StringComparer.Ordinal);
        for (var c = 'a'; c <= 'z'; c++) map[c.ToString()] = (ushort)(0x41 + (c - 'a'));
        for (var d = 0; d <= 9; d++)
        {
            map[d.ToString()] = (ushort)(0x30 + d);
            map[$"num{d}"] = (ushort)(0x60 + d);
        }
        for (var f = 1; f <= 24; f++) map[$"f{f}"] = (ushort)(0x70 + f - 1);

        map["ctrl"] = 0x11; map["shift"] = 0x10; map["alt"] = 0x12; map["win"] = 0x5B;
        map["enter"] = 0x0D; map["escape"] = 0x1B; map["tab"] = 0x09; map["space"] = 0x20;
        map["backspace"] = 0x08; map["delete"] = 0x2E; map["insert"] = 0x2D;
        map["home"] = 0x24; map["end"] = 0x23; map["pageup"] = 0x21; map["pagedown"] = 0x22;
        map["up"] = 0x26; map["down"] = 0x28; map["left"] = 0x25; map["right"] = 0x27;
        map["printscreen"] = 0x2C; map["pause"] = 0x13; map["capslock"] = 0x14;
        map["numlock"] = 0x90; map["scrolllock"] = 0x91; map["menu"] = 0x5D;
        map["numadd"] = 0x6B; map["numsubtract"] = 0x6D; map["nummultiply"] = 0x6A;
        map["numdivide"] = 0x6F; map["numdecimal"] = 0x6E;

        // OEM keys: the character they produce depends on the active keyboard layout (US names used here).
        map["plus"] = 0xBB; map["equal"] = 0xBB; map["minus"] = 0xBD; map["comma"] = 0xBC;
        map["period"] = 0xBE; map["semicolon"] = 0xBA; map["slash"] = 0xBF; map["backslash"] = 0xDC;
        map["quote"] = 0xDE; map["backquote"] = 0xC0; map["bracketleft"] = 0xDB; map["bracketright"] = 0xDD;

        map["volumemute"] = 0xAD; map["volumedown"] = 0xAE; map["volumeup"] = 0xAF;
        map["medianext"] = 0xB0; map["mediaprev"] = 0xB1; map["mediastop"] = 0xB2; map["mediaplaypause"] = 0xB3;
        return map;
    }
}
