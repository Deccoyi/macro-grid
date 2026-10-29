namespace MacroGrid.Core.Diagnostics;

/// <summary>When the crash report window shows at the next start.</summary>
public enum CrashReportTiming
{
    /// <summary>No crash was found.</summary>
    None,
    /// <summary>The crash came while starting: shown before the server starts, even on autostart.</summary>
    BeforeServer,
    /// <summary>The editor opens at start: shown right after it opens.</summary>
    WithEditor,
    /// <summary>Tray only: a balloon says a report is ready; the window opens from the tray menu or the balloon.</summary>
    OnTrayMenu,
}

public static class CrashReportPolicy
{
    /// <summary>Decides from the markers found. An unreadable marker counts as <c>running</c>, because its phase is unknown.</summary>
    public static CrashReportTiming When(CrashMarkerSet set, bool editorOpensAtStart)
    {
        if (set.Markers.Count == 0 && set.Unreadable.Count == 0) return CrashReportTiming.None;
        if (set.Markers.Any(m => m.Phase == CrashPhase.Starting)) return CrashReportTiming.BeforeServer;
        return editorOpensAtStart ? CrashReportTiming.WithEditor : CrashReportTiming.OnTrayMenu;
    }
}
