namespace MacroStation.Core.Sessions;

/// <summary>The foreground window as process name + title. <see cref="ProcessName"/> has no ".exe"
/// stripped/added expectation — callers compare it exactly as reported, case-insensitively.</summary>
public sealed record ForegroundWindow(string ProcessName, string Title);

/// <summary>
/// Reports which process/window is in the foreground on the server machine — implemented by
/// <c>MacroStation.Windows.ForegroundWindowMonitor</c> (a Win32 <c>SetWinEventHook</c>), consumed by
/// <see cref="AutoProfileSwitcher"/>. Kept as a Core-only interface (not in the plugin SDK) so it can be
/// faked in tests without any Windows/P-Invoke dependency.
/// </summary>
public interface IActiveWindowSource
{
    /// <summary>Fires whenever a different top-level window becomes the foreground window.</summary>
    event Action<ForegroundWindow>? ForegroundChanged;

    /// <summary>True if the named process currently has at least one visible top-level window — used to
    /// detect "the app closed" (including one that minimized to the tray) without waiting for a
    /// foreground-change event that may never come (nothing else was clicked into focus).</summary>
    bool HasVisibleWindow(string processName);

    /// <summary>Starts watching. Safe to call once; the monitor runs for the process lifetime.</summary>
    void Start();

    /// <summary>Every process currently owning at least one visible top-level window, one entry per
    /// process (its main/first visible window's title) — for the editor's "çalışan uygulamadan seç" list.
    /// Not filtered against MacroStation's own process; the editor excludes it itself if it shows up.</summary>
    IReadOnlyList<ForegroundWindow> ListVisibleWindows();
}
