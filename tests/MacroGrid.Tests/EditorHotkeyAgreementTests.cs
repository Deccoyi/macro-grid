using System.Text.RegularExpressions;
using MacroGrid.Core.Input;

namespace MacroGrid.Tests;

/// <summary>The editor captures a Hotkey field with keyCapture.ts; the server reads the saved text with <see cref="HotkeyParser"/>. Every name the editor can write must be one the parser accepts.</summary>
public sealed class EditorHotkeyAgreementTests
{
    private static string KeyCaptureSource()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "editor", "src", "panels", "actionForms", "keyCapture.ts");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("keyCapture.ts not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Every_key_name_the_editor_captures_is_accepted_by_the_parser()
    {
        var source = KeyCaptureSource();
        var table = Regex.Match(source, @"CODE_TO_NAME[^=]*=\s*\{(?<body>.*?)\};", RegexOptions.Singleline).Groups["body"].Value;
        var names = Regex.Matches(table, @":\s*""(?<n>[a-z0-9]+)""").Select(m => m.Groups["n"].Value).Distinct().ToList();
        // Names built from patterns in codeToKeyName: letters, digits, num0-9, f1-f24, and the four modifiers.
        names.AddRange(Enumerable.Range('a', 26).Select(c => ((char)c).ToString()));
        names.AddRange(Enumerable.Range(0, 10).SelectMany(d => new[] { d.ToString(), $"num{d}" }));
        names.AddRange(Enumerable.Range(1, 24).Select(f => $"f{f}"));

        Assert.True(names.Count > 60);
        foreach (var name in names)
            Assert.True(HotkeyParser.TryParse(name, out _, out var error), $"'{name}': {error}");
        foreach (var modifier in new[] { "ctrl", "shift", "alt", "win" })
            Assert.True(HotkeyParser.TryParse($"{modifier}+a", out _, out var error), $"'{modifier}': {error}");
        Assert.True(HotkeyParser.TryParse("ctrl+shift+alt+win+s", out _, out _));
    }
}
