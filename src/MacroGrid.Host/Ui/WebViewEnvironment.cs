using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MacroGrid.Host.Ui;

/// <summary>
/// Starts the WebView2 control of the editor and of every tool window. WebView2 needs a user data folder it can write to.
/// Left at its default it sits next to the exe (<c>MacroGrid.exe.WebView2</c>), which is under Program Files after an install
/// and not writable for a normal user (E_ACCESSDENIED, 0x80070005). The folders are tried in this order and the first one
/// that starts wins: local application data, roaming application data (the profile store already writes there), the
/// temp folder. When none works, the message names every folder with its error and the same text is written to
/// <c>%TEMP%\MacroGrid-webview-error.txt</c>, because on a locked-down machine that is the only clue there is.
/// </summary>
internal static class WebViewEnvironment
{
    private static string[] CandidateFolders() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacroGrid", "WebView2"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacroGrid", "WebView2"),
        Path.Combine(Path.GetTempPath(), "MacroGrid", "WebView2"),
    ];

    // The folder that worked once, reused by every later window so they all share one browser environment.
    private static string? _workingFolder;

    /// <summary>Creates a WebView2 control inside <paramref name="host"/> and navigates it to <paramref name="url"/>. With
    /// <paramref name="followDocumentTitle"/> the window title tracks the page's document title.
    /// Call it from the UI thread. Shows an error dialog when no user data folder works.</summary>
    public static async Task AttachAsync(Form host, string url, bool followDocumentTitle = true)
    {
        var failures = new StringBuilder();
        var folders = _workingFolder is null ? CandidateFolders() : [_workingFolder];

        foreach (var folder in folders)
        {
            if (host.IsDisposed) return;
            var view = new WebView2 { Dock = DockStyle.Fill };
            try
            {
                Directory.CreateDirectory(folder);
                host.Controls.Add(view);
                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null, userDataFolder: folder,
                    options: new CoreWebView2EnvironmentOptions { Language = HostText.Language == "tr" ? "tr-TR" : "en-US" });
                await view.EnsureCoreWebView2Async(environment);
                // The editor sets document.title from the language preference, so the native title bar follows it.
                if (followDocumentTitle)
                {
                    view.CoreWebView2.DocumentTitleChanged += (_, _) =>
                    {
                        if (!host.IsDisposed && !string.IsNullOrWhiteSpace(view.CoreWebView2.DocumentTitle))
                            host.Text = view.CoreWebView2.DocumentTitle;
                    };
                }
                // A page that calls window.close() (the update window after "Later" or "Skip") closes its own native window.
                view.CoreWebView2.WindowCloseRequested += (_, _) =>
                {
                    if (!host.IsDisposed) host.Close();
                };
                view.CoreWebView2.ContextMenuRequested += (_, e) => ShowLocalizedContextMenu(view, e);
                view.CoreWebView2.Navigate(url);
                _workingFolder = folder;
                return;
            }
            catch (Exception ex)
            {
                failures.AppendLine($"{folder}\n    {ex.GetType().Name} (0x{ex.HResult:X8}): {ex.Message}");
                host.Controls.Remove(view);
                view.Dispose();
            }
        }

        if (host.IsDisposed) return;

        var report = $"Macro Grid {DateTime.Now:yyyy-MM-dd HH:mm:ss}, user {Environment.UserName}, elevated: {IsElevated()}\n" +
                     $"Program folder: {AppContext.BaseDirectory}\nWebView2 did not start with any user data folder:\n{failures}";
        try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "MacroGrid-webview-error.txt"), report); }
        catch (Exception) { /* the dialog below still shows it */ }

        MessageBox.Show(
            host,
            HostText.Get("webview.missing") +
            "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" + report +
            "\n" + HostText.Get("webview.writtenTo", Path.Combine(Path.GetTempPath(), "MacroGrid-webview-error.txt")),
            "Macro Grid",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    // WebView2 draws its right-click menu in the language it was started with and cannot switch it while running, so the
    // menu is drawn here instead: only the editing and navigation commands, named from HostText (the menu follows the
    // language preference live). Anything else the browser offers (developer tools, save as, print, ...) is left out.
    private static void ShowLocalizedContextMenu(WebView2 view, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        var entries = new List<(string? Text, int CommandId, bool Enabled)>();
        foreach (var item in e.MenuItems)
        {
            if (item.Kind == CoreWebView2ContextMenuItemKind.Separator)
            {
                if (entries.Count > 0 && entries[^1].Text is not null) entries.Add((null, 0, false));
                continue;
            }
            var key = "menu." + item.Name;
            if (item.Kind != CoreWebView2ContextMenuItemKind.Command || !HostText.Has(key)) continue;
            entries.Add((HostText.Get(key), item.CommandId, item.IsEnabled));
        }
        while (entries.Count > 0 && entries[^1].Text is null) entries.RemoveAt(entries.Count - 1);

        e.Handled = true;
        if (entries.Count == 0) return;

        var deferral = e.GetDeferral();
        var menu = new ContextMenuStrip();
        foreach (var (text, commandId, enabled) in entries)
        {
            if (text is null) { menu.Items.Add(new ToolStripSeparator()); continue; }
            var id = commandId;
            menu.Items.Add(new ToolStripMenuItem(text, null, (_, _) => e.SelectedCommandId = id) { Enabled = enabled });
        }
        // Closed is raised before the click of the chosen item is handled; completing later keeps the choice.
        menu.Closed += (_, _) => view.BeginInvoke(() => { deferral.Complete(); menu.Dispose(); });
        menu.Show(view, e.Location);
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch (Exception) { return false; }
    }
}
