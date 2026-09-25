using System.Diagnostics;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;

namespace MacroGrid.Host.Ui;

/// <summary>Owns the tray icon; the application lives as long as this context.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _clientsItem;
    private readonly ToolStripMenuItem _openEditorItem;
    private readonly ToolStripMenuItem _openTestPageItem;
    private readonly ToolStripMenuItem _openDataFolderItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly ClientHub _hub;
    private readonly SynchronizationContext _ui;

    public TrayContext(WebApplication server, bool openEditor)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _hub = server.Services.GetRequiredService<ClientHub>();

        var addresses = NetworkInfo.GetLanAddresses();
        var addressText = addresses.Count > 0
            ? string.Join(", ", addresses.Select(a => $"{a}:{ServerApp.Port}"))
            : $"localhost:{ServerApp.Port}";

        _clientsItem = new ToolStripMenuItem { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"Macro Grid {ClientHub.ServerVersion}") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem(addressText) { Enabled = false });
        menu.Items.Add(_clientsItem);
        menu.Items.Add(new ToolStripSeparator());
        _openEditorItem = new ToolStripMenuItem(null, null, (_, _) => OpenEditor());
        _openTestPageItem = new ToolStripMenuItem(null, null, (_, _) => Open($"http://localhost:{ServerApp.Port}/"));
        _openDataFolderItem = new ToolStripMenuItem(null, null, (_, _) => Open(ProfileStore.DefaultDataDir));
        _exitItem = new ToolStripMenuItem(null, null, (_, _) => ExitThread());
        menu.Items.Add(_openEditorItem);
        menu.Items.Add(_openTestPageItem);
        menu.Items.Add(_openDataFolderItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "Macro Grid",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenEditor();

        _hub.SessionsChanged += () => _ui.Post(_ => UpdateClients(), null);
        // The menu texts follow the language preference, also while the app is running.
        server.Services.GetRequiredService<PreferencesStore>().Changed += () => _ui.Post(_ => ApplyLanguage(), null);
        ApplyLanguage();

        if (openEditor)
            OpenEditor();
        else
            _icon.ShowBalloonTip(3000, HostText.Get("tray.running"), HostText.Get("tray.connectFrom", addressText), ToolTipIcon.Info);
    }

    private void ApplyLanguage()
    {
        _openEditorItem.Text = HostText.Get("tray.openEditor");
        _openTestPageItem.Text = HostText.Get("tray.openTestPage");
        _openDataFolderItem.Text = HostText.Get("tray.openDataFolder");
        _exitItem.Text = HostText.Get("tray.exit");
        UpdateClients();
    }

    private void UpdateClients()
    {
        var sessions = _hub.Sessions.Where(s => s.IsIdentified).ToList();
        _clientsItem.Text = sessions.Count == 0
            ? HostText.Get("tray.noClients")
            : HostText.Get("tray.clients", string.Join(", ", sessions.Select(s => s.DeviceName)));
    }

    private static void OpenEditor() => EditorWindow.ShowOrFocus($"http://localhost:{ServerApp.Port}/editor/");

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    protected override void ExitThreadCore()
    {
        _icon.Visible = false;
        _icon.Dispose();
        base.ExitThreadCore();
    }
}
