using Microsoft.Web.WebView2.WinForms;

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
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };

    private EditorWindow(string url)
    {
        Text = "Macro Grid Editörü";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Controls.Add(_webView);
        _ = InitializeAsync(url);
    }

    private async Task InitializeAsync(string url)
    {
        try
        {
            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "WebView2 çalışma zamanı başlatılamadı. Microsoft Edge WebView2 Runtime kurulu olmalı:\nhttps://developer.microsoft.com/microsoft-edge/webview2/\n\n" + ex.Message,
                "Macro Grid",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
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
