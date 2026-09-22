using System.Diagnostics;
using MacroStation.Core.Profiles;
using MacroStation.Core.Sessions;

namespace MacroStation.Host;

/// <summary>Owns the tray icon; the application lives as long as this context.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _clientsItem;
    private readonly ClientHub _hub;
    private readonly SynchronizationContext _ui;

    public TrayContext(WebApplication server)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _hub = server.Services.GetRequiredService<ClientHub>();

        var addresses = NetworkInfo.GetLanAddresses();
        var addressText = addresses.Count > 0
            ? string.Join(", ", addresses.Select(a => $"{a}:{ServerApp.Port}"))
            : $"localhost:{ServerApp.Port}";

        _clientsItem = new ToolStripMenuItem { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"Macro Station {ClientHub.ServerVersion}") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem(addressText) { Enabled = false });
        menu.Items.Add(_clientsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Düzenleyiciyi aç", null, (_, _) => OpenEditor());
        menu.Items.Add("Test sayfasını aç", null, (_, _) => Open($"http://localhost:{ServerApp.Port}/"));
        menu.Items.Add("Veri klasörünü aç", null, (_, _) => Open(ProfileStore.DefaultDataDir));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Macro Station",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenEditor();

        _hub.SessionsChanged += () => _ui.Post(_ => UpdateClients(), null);
        UpdateClients();

        _icon.ShowBalloonTip(3000, "Macro Station çalışıyor", $"Telefondan bağlan: {addressText}", ToolTipIcon.Info);
    }

    private void UpdateClients()
    {
        var sessions = _hub.Sessions.Where(s => s.IsIdentified).ToList();
        _clientsItem.Text = sessions.Count == 0
            ? "Bağlı cihaz yok"
            : $"Bağlı: {string.Join(", ", sessions.Select(s => s.DeviceName))}";
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
