using System.ComponentModel;
using System.Runtime.InteropServices;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Windows.Input;

/// <summary>Keyboard simulation through Win32 SendInput.</summary>
public sealed class WindowsInputService : IInputService
{
    private static readonly (KeyModifiers Flag, string Key)[] ModifierKeys =
    [
        (KeyModifiers.Ctrl, "ctrl"), (KeyModifiers.Shift, "shift"), (KeyModifiers.Alt, "alt"), (KeyModifiers.Win, "win"),
    ];

    public void SendKeyCombo(KeyCombo combo)
    {
        if (!VirtualKeys.Map.ContainsKey(combo.Key))
            throw new ArgumentException($"No virtual-key mapping for '{combo.Key}'.", nameof(combo));

        var inputs = new List<INPUT>();
        var mods = ModifierKeys.Where(m => combo.Modifiers.HasFlag(m.Flag)).Select(m => m.Key).ToList();

        // Press modifiers, tap the key, release modifiers in reverse order.
        foreach (var m in mods) inputs.Add(Key(m, up: false));
        inputs.Add(Key(combo.Key, up: false));
        inputs.Add(Key(combo.Key, up: true));
        for (var i = mods.Count - 1; i >= 0; i--) inputs.Add(Key(mods[i], up: true));

        Send(inputs);
    }

    public void TypeText(string text)
    {
        // KEYEVENTF_UNICODE types any character (ş, ğ, emoji surrogates) regardless of keyboard layout.
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (var ch in text.ReplaceLineEndings("\r"))
        {
            inputs.Add(Unicode(ch, up: false));
            inputs.Add(Unicode(ch, up: true));
        }
        Send(inputs);
    }

    private static INPUT Key(string name, bool up)
    {
        var flags = up ? KEYEVENTF_KEYUP : 0u;
        if (VirtualKeys.Extended.Contains(name)) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = VirtualKeys.Map[name], dwFlags = flags } },
        };
    }

    private static INPUT Unicode(char ch, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0u) } },
    };

    private static void Send(List<INPUT> inputs)
    {
        if (inputs.Count == 0) return;
        var array = inputs.ToArray();
        var sent = SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
        if (sent != array.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput was blocked (UIPI: target window may be running as administrator).");
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // The union must include MOUSEINPUT (the largest member) so Marshal.SizeOf matches the native struct.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }
}
