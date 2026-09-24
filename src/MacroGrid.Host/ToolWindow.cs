namespace MacroGrid.Host;

/// <summary>
/// A tool window (Tercihler, Eklentiler, ...) — a real separate OS window with its own WebView2 and native
/// title bar, not an in-page modal overlay (see docs/ui-guidelines.md: desktop apps show Preferences as
/// its own window, never a dimmed dialog on top of the canvas). Like an ordinary dialog it is owned by the editor
/// window, stays above it and blocks it while open: the editor is disabled, so clicking it plays the Windows
/// warning sound and flashes the tool window instead of working behind it. This is done with Owner plus
/// Enabled = false rather than ShowDialog, so no nested message loop runs inside the API call that opens the window.
/// One instance per `kind` at a time; reopening focuses the existing one instead of spawning a duplicate.
/// </summary>
internal sealed class ToolWindow : Form
{
    private static readonly Dictionary<string, ToolWindow> Instances = [];

    // Number of open tool windows that block the editor; it is enabled again when the last one closes.
    private static int _blockingCount;
    private bool _blocksEditor;

    private ToolWindow(string title, string url, int width, int height)
    {
        Text = title;
        Width = width;
        Height = height;
        MinimumSize = new Size(480, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        // A plugin's settings window is titled with the plugin's name, which the page does not know.
        _ = WebViewEnvironment.AttachAsync(this, url, followDocumentTitle: !url.Contains("window=plugin-settings"));
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

        var editor = EditorWindow.Current;
        if (editor is null)
        {
            window.Show();
            return;
        }

        // Block the editor while the window is open, and give it back before this window disappears so the editor
        // (not some other program) comes to the front when the last tool window closes.
        window._blocksEditor = true;
        _blockingCount++;
        editor.Enabled = false;
        window.FormClosing += (_, _) => window.ReleaseEditor();
        window.Show(editor);
    }

    private void ReleaseEditor()
    {
        if (!_blocksEditor) return;
        _blocksEditor = false;
        if (--_blockingCount > 0) return;

        if (EditorWindow.Current is { } editor)
        {
            editor.Enabled = true;
            editor.Activate();
        }
    }
}
