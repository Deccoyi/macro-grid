namespace MacroGrid.Host;

/// <summary>
/// The editor lives only here, never in the system's default browser (a deliberate product
/// decision: it's part of the desktop app, not a website). One instance at a time — reopening
/// while it's already open just focuses the existing window instead of navigating a fresh copy,
/// so in-progress edits are never silently discarded.
/// </summary>
internal sealed class EditorWindow : Form
{
    private static EditorWindow? _instance;

    /// <summary>The open editor window, or null. Tool windows use it as their owner (see <see cref="ToolWindow"/>).</summary>
    internal static EditorWindow? Current => _instance is { IsDisposed: false } ? _instance : null;

    private EditorWindow(string url)
    {
        Text = "Macro Grid Editörü";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _ = WebViewEnvironment.AttachAsync(this, url);
    }


    public static void ShowOrFocus(string url)
    {
        if (_instance is { IsDisposed: false })
        {
            if (_instance.WindowState == FormWindowState.Minimized)
                _instance.WindowState = FormWindowState.Normal;
            _instance.Activate();
            return;
        }

        _instance = new EditorWindow(url);
        _instance.FormClosed += (_, _) =>
        {
            _instance = null;
            // A Tercihler/Eklentiler window left open with no editor behind it would be confusing.
            ToolWindow.CloseAll();
        };
        _instance.Show();
    }
}
