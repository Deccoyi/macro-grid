using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MacroGrid.Core.Sessions;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>What one button press has done with the keyboard so far. A press window is opened for an action a person
/// started by touching a device; it ends when the action's job and its promise have settled or after
/// <see cref="JsPluginLimits.PressWindow"/>, whichever comes first.</summary>
internal sealed class JsPressWindow(long deadline)
{
    public long Deadline { get; } = deadline;
    public bool Settled { get; set; }
    public int TypedChars { get; set; }
    public int KeyCombos { get; set; }
    public bool Counted { get; set; }

    /// <summary>Everything typed in this press, so text split over several calls is checked as one.</summary>
    public StringBuilder Text { get; } = new();
}

/// <summary>
/// The rules that limit what a JavaScript plugin's <c>input</c> permission can do, on top of the person's approval. They
/// apply only to JavaScript plugins; the built-in hotkey and type-text actions, which the person configures themselves, are
/// unchanged. What each rule is worth, honestly:
/// <list type="bullet">
/// <item>Only while handling a real press, few keys per press, no combinations that open the system, and no typing into a
/// terminal, a script host, a system tool or Macro Grid's own windows: these stop hidden and large-scale abuse.</item>
/// <item>The text blocklist is the weak rule. It can be worked around (spelling tricks, typing into another program) and can
/// refuse legitimate text; it only catches crude and copied attacks.</item>
/// </list>
/// An approved plugin with <c>input</c> can still type up to <see cref="MaxTypedCharsPerPress"/> characters into an ordinary
/// program when the person presses its button. The approval is the real decision.
/// </summary>
internal static class JsInputPolicy
{
    public const int MaxTypedCharsPerPress = 200;
    public const int MaxKeyCombosPerPress = 10;
    public const int MaxCharsPerTypeCall = 200;

    // The one list of windows keyboard input is never sent to. Process names are compared case-insensitively as reported by
    // the window source (with ".exe"). Input to an administrator window is blocked by Windows anyway, and when Macro Grid
    // itself runs as administrator JavaScript input is refused entirely (see JsPlugin).
    private static readonly HashSet<string> RefusedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // command prompts, shells and terminal apps
        "cmd.exe", "powershell.exe", "powershell_ise.exe", "pwsh.exe", "wt.exe", "windowsterminal.exe", "conhost.exe",
        "openconsole.exe", "bash.exe", "wsl.exe", "wslhost.exe", "mintty.exe", "alacritty.exe",
        // script hosts
        "wscript.exe", "cscript.exe", "mshta.exe",
        // the registry editor, system management consoles and system tools
        "regedit.exe", "mmc.exe", "msconfig.exe", "taskmgr.exe", "control.exe", "systemsettings.exe",
        // the Start menu and search, and the secure desktop screens
        "startmenuexperiencehost.exe", "searchhost.exe", "searchapp.exe", "searchui.exe", "shellexperiencehost.exe",
        "consent.exe", "logonui.exe", "winlogon.exe",
    };

    // Windows that host a console or terminal whatever process owns them.
    private static readonly HashSet<string> RefusedClasses = new(StringComparer.Ordinal)
    {
        "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "PseudoConsoleWindow",
    };

    // Macro Grid's own windows (the editor and its dialogs): a plugin must never click through its own approval.
    private static readonly string OwnProcess = Process.GetCurrentProcess().ProcessName + ".exe";

    /// <summary>The reason input to this foreground window is refused, or null when it may receive it. Unknown counts as refused.</summary>
    public static string? RefuseTarget(ForegroundWindow? foreground)
    {
        if (foreground is null) return "Keyboard input is refused because the window in front could not be identified.";
        var process = foreground.ProcessName;
        if (string.Equals(process, OwnProcess, StringComparison.OrdinalIgnoreCase)
            || string.Equals(process, "MacroGrid.exe", StringComparison.OrdinalIgnoreCase))
            return "Keyboard input is refused while a Macro Grid window is in front.";
        if (RefusedProcesses.Contains(process) || RefusedClasses.Contains(foreground.WindowClass))
            return "Keyboard input is refused while a terminal, script host or system tool is in front.";
        // The Run dialog is an ordinary dialog of the shell; every shell dialog is refused with it.
        if (string.Equals(process, "explorer.exe", StringComparison.OrdinalIgnoreCase) && foreground.WindowClass == "#32770")
            return "Keyboard input is refused while a system dialog is in front.";
        return null;
    }

    /// <summary>The reason a key combination is refused, or null. Anything with the Windows key (Start, Run, the power-user
    /// menu, settings) and the ones that open Task Manager or the security screen. The person can still use them through the
    /// built-in hotkey action.</summary>
    public static string? RefuseCombo(KeyCombo combo)
    {
        if (combo.Modifiers.HasFlag(KeyModifiers.Win) || combo.Key.Equals("win", StringComparison.OrdinalIgnoreCase))
            return "Key combinations with the Windows key are not allowed.";
        var ctrl = combo.Modifiers.HasFlag(KeyModifiers.Ctrl);
        var key = combo.Key.ToLowerInvariant();
        if ((ctrl && key == "escape") || (ctrl && combo.Modifiers.HasFlag(KeyModifiers.Alt) && key == "delete"))
            return "This key combination opens a system screen and is not allowed.";
        return null;
    }

    /// <summary>Lowercase, runs of spaces collapsed, and the shell's escape characters and quotes removed, so a trivially
    /// disguised command still matches.</summary>
    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '^' or '`' or '"' or '\'') continue;
            sb.Append(char.IsWhiteSpace(c) ? ' ' : char.ToLowerInvariant(c));
        }
        return Regex.Replace(sb.ToString(), " {2,}", " ");
    }

    private static readonly (string Name, Regex Pattern)[] BlockedText =
    [
        ("start-shell-or-script-host", Rx(@"\b(cmd|powershell|pwsh|wscript|cscript|mshta|rundll32|regsvr32|wmic)(\.exe)?\b")),
        ("download-and-run", Rx(@"\b(invoke-webrequest|invoke-restmethod|invoke-expression|iwr|iex|downloadstring|downloadfile|start-bitstransfer|bitsadmin)\b|\bcertutil\b.*\burlcache\b|\b(curl|wget)\b.*https?:")),
        ("encoded-command", Rx(@"\s-e(nc|ncodedcommand)?\s+[a-z0-9+/=]{16,}|\bfrombase64string\b")),
        ("registry", Rx(@"\breg(\.exe)?\s+(add|delete|import|save|load|unload)\b|\bregedit\b|\bhkey_[a-z_]+|\bhk(lm|cu|cr)\b")),
        ("services-tasks-users-firewall", Rx(@"\bsc(\.exe)?\s+(create|config|delete|stop|start)\b|\bnet\s+(user|localgroup|stop|start)\b|\bschtasks\b|\bnetsh\b|\b(new|set|remove)-service\b|\bregister-scheduledtask\b|\b(new-localuser|add-localgroupmember)\b")),
        ("delete-or-format-drives", Rx(@"\bformat\s+[a-z]:|\b(rd|rmdir)\s+/s\b|\bdel\s+/[fsq]\b|\brm\s+-r?f\b|\bremove-item\b.*-recurse|\bdiskpart\b|\bcipher\s+/w\b")),
    ];

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    /// <summary>The name of the blocklist rule the (already normalized) text matches, or null. The text itself is never
    /// reported, only the rule's name.</summary>
    public static string? MatchBlockedText(string normalized)
    {
        foreach (var (name, pattern) in BlockedText)
        {
            try { if (pattern.IsMatch(normalized)) return name; }
            catch (RegexMatchTimeoutException) { return name; } // a pattern that cannot be decided in time counts as a match
        }
        return null;
    }

    /// <summary>True when this process runs with administrator rights.</summary>
    public static bool ServerIsElevated()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
