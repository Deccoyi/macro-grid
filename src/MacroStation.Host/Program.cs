namespace MacroStation.Host;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\MacroStation.Server", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Macro Station zaten çalışıyor (sistem tepsisine bakın).", "Macro Station",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        // Captured here (on the real UI thread, before the message loop even starts) so API handlers
        // running on Kestrel's thread pool can still marshal a native dialog (OpenFileDialog, ...) onto it.
        var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        var dialogs = new UiDialogService(ui);

        var server = ServerApp.Build(args, dialogs);
        try
        {
            server.StartAsync().GetAwaiter().GetResult();
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Sunucu {ServerApp.Port} portunda başlatılamadı:\n{ex.Message}", "Macro Station",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new TrayContext(server));

        server.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
    }
}
