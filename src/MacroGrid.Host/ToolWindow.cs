namespace MacroGrid.Host;

/// <summary>
/// A floating, non-modal tool window (Tercihler, Eklentiler, ...) — a real separate OS window with its
/// own WebView2 and native title bar, not an in-page modal overlay (see docs/ui-guidelines.md: desktop
/// apps commonly show Preferences as its own window, never a dimmed dialog on top of the canvas). Kept
/// non-modal on purpose — the editor stays usable while this is open. One instance per `kind` at a time;
/// reopening focuses the existing one instead of spawning a duplicate.
/// </summary>
internal sealed class ToolWindow : Form
{
    private static readonly Dictionary<string, ToolWindow> Instances = [];

    private ToolWindow(string title, string url, int width, int height)
    {
        Text = title;
        Width = width;
        Height = height;
        MinimumSize = new Size(480, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _ = WebViewEnvironment.AttachAsync(this, url);
    }


    /// <summary>Closes every open tool window — called when the main editor window closes, since a
    /// Preferences/Plugins window left dangling with no editor behind it would be confusing.</summary>
    public static void CloseAll()
    {
        foreach (var window in Instances.Values.ToList())
            window.Close();
    }

    public static void ShowOrFocus(string kind, string title, string url, int width, int height)
    {
        if (Instances.TryGetValue(kind, out var existing) && existing is { IsDisposed: false })
        {
            if (existing.WindowState == FormWindowState.Minimized)
                existing.WindowState = FormWindowState.Normal;
            existing.Activate();
            return;
        }

        var window = new ToolWindow(title, url, width, height);
        window.FormClosed += (_, _) => Instances.Remove(kind);
        Instances[kind] = window;
        window.Show();
    }
}
