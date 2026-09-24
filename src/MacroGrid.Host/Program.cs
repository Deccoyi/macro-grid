using MacroGrid.Core.Preferences;
using MacroGrid.Host.Ui;

namespace MacroGrid.Host;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\MacroGrid.Server", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(HostText.Get("app.alreadyRunning"), "Macro Grid",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        // Captured here (on the real UI thread, before the message loop even starts) so API handlers
        // running on Kestrel's thread pool can still marshal a native dialog (OpenFileDialog, ...) onto it.
        var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        var dialogs = new UiDialogService(ui);
        var windows = new UiWindowService(ui);

        var server = ServerApp.Build(args, dialogs, windows);
        HostText.Bind(server.Services.GetRequiredService<PreferencesStore>());
        try
        {
            server.StartAsync().GetAwaiter().GetResult();
        }
        catch (IOException ex)
        {
            MessageBox.Show(HostText.Get("server.startFailed", ServerApp.Port, ex.Message), "Macro Grid",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var openEditor = StartupPolicy.ShouldOpenEditor(args, server.Services.GetRequiredService<PreferencesStore>().Get());
        Application.Run(new TrayContext(server, openEditor));

        // The tray icon is gone at this point. If stopping the server hangs (for example a hosted service waiting for
        // the UI thread, whose message loop has just ended) or a foreground thread survives, the process would stay
        // in the background with no icon to quit it from. This watchdog ends the process in that case.
        new Thread(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(8));
            Environment.Exit(0);
        }) { IsBackground = true }.Start();

        try
        {
            server.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Shutting down anyway.
        }

        Environment.Exit(0);
    }
}
