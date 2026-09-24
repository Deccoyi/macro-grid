using System.Diagnostics.CodeAnalysis;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Input;

/// <summary>
/// Parses user-written shortcuts like "ctrl+shift+s", "Alt+F4", "win" or "ctrl+plus".
/// A literal plus key is written as "plus" because '+' is the separator.
/// </summary>
public static class HotkeyParser
{
    private static readonly Dictionary<string, KeyModifiers> ModifierAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = KeyModifiers.Ctrl, ["control"] = KeyModifiers.Ctrl,
        ["shift"] = KeyModifiers.Shift,
        ["alt"] = KeyModifiers.Alt, ["option"] = KeyModifiers.Alt,
        ["win"] = KeyModifiers.Win, ["windows"] = KeyModifiers.Win, ["meta"] = KeyModifiers.Win, ["cmd"] = KeyModifiers.Win,
    };

    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["esc"] = "escape", ["return"] = "enter", ["del"] = "delete", ["ins"] = "insert",
        ["pgup"] = "pageup", ["pgdn"] = "pagedown", ["prtsc"] = "printscreen", ["bksp"] = "backspace",
        ["arrowup"] = "up", ["arrowdown"] = "down", ["arrowleft"] = "left", ["arrowright"] = "right",
    };

    public static KeyCombo Parse(string text) =>
        TryParse(text, out var combo, out var error) ? combo : throw new FormatException(error);

    public static bool TryParse(string? text, [NotNullWhen(true)] out KeyCombo? combo, out string? error)
    {
        combo = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Kısayol boş.";
            return false;
        }

        var tokens = text.Split('+', StringSplitOptions.TrimEntries);
        if (tokens.Any(t => t.Length == 0))
        {
            error = $"Geçersiz kısayol: '{text}'. '+' tuşu için 'plus' yazın.";
            return false;
        }

        var modifiers = KeyModifiers.None;
        string? key = null;
        foreach (var raw in tokens)
        {
            if (ModifierAliases.TryGetValue(raw, out var mod))
            {
                modifiers |= mod;
                continue;
            }

            if (key is not null)
            {
                error = $"Birden fazla tuş var: '{key}' ve '{raw}'.";
                return false;
            }

            var normalized = raw.ToLowerInvariant();
            key = KeyAliases.GetValueOrDefault(normalized, normalized);
            if (!KnownKeys.All.Contains(key))
            {
                error = $"Bilinmeyen tuş: '{raw}'.";
                return false;
            }
        }

        // A lone modifier (e.g. "win") is sent as a plain key press.
        if (key is null)
        {
            if (tokens.Length != 1)
            {
                error = "Kısayolda modifier dışında bir tuş olmalı.";
                return false;
            }
            key = ModifierAliases[tokens[0]].ToString().ToLowerInvariant();
            modifiers = KeyModifiers.None;
        }

        combo = new KeyCombo(modifiers, key);
        return true;
    }
}
