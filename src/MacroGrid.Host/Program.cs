using MacroGrid.Core.Backup;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Updates;
using MacroGrid.Host.Ui;
using MacroGrid.Host.Updates;
using MacroGrid.Windows.Agreement;

namespace MacroGrid.Host;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // First: a crash anywhere after this line leaves a marker for the next start (see CrashCapture).
        CrashCapture.Install(ProfileStore.DefaultDataDir);

        using var mutex = new Mutex(initiallyOwned: true, @"Local\MacroGrid.Server", out var isFirstInstance);
#if DEBUG
        if (Environment.GetEnvironmentVariable("MACROGRID_ALLOW_SECOND_INSTANCE") == "1")
            isFirstInstance = true;
#endif
        if (!isFirstInstance)
        {
            MessageBox.Show(HostText.Get("app.alreadyRunning"), "Macro Grid",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        // The person has to have accepted the user agreement this copy ships before anything runs (see AgreementGate); declining ends the app here.
        if (!AgreementGate.EnsureAccepted(AppContext.BaseDirectory, new AgreementRecord()))
            return;

        // A crash last time: the report text is built before anything is deleted, and the marker files are deleted before the window shows,
        // so a crash inside the window or in the next run can never show the same report again and again.
        var dataDir = ProfileStore.DefaultDataDir;
        var crashMarkers = CrashMarkers.Read(Path.Combine(dataDir, CrashMarkers.FolderName));
        PendingCrashReport? crashReport = null;
        Exception? crashReportError = null;
        if (crashMarkers.Files.Count > 0)
        {
            try
            {
                var redactor = new Redactor(RedactionContext.ForThisPc(dataDir, DeviceStore.ReadNames(dataDir)), minNameLength: 2);
                crashReport = new PendingCrashReport(
                    CrashReport.Build(crashMarkers, Path.Combine(dataDir, "logs"), redactor, AppContext.BaseDirectory), crashMarkers.Files);
            }
            catch (Exception ex)
            {
                // A broken report must never stop the app: it is logged below, the markers go, and the app starts normally.
                crashReportError = ex;
                CrashMarkers.Delete(crashMarkers.Files);
            }
        }

        // A crash while starting is shown before the server starts, even on autostart: a start that crashes every time never reaches the tray.
        if (crashReport is not null && CrashReportPolicy.When(crashMarkers, editorOpensAtStart: false) == CrashReportTiming.BeforeServer)
        {
            CrashMarkers.Delete(crashReport.Files);
            using (var dialog = new CrashReportDialog(crashReport.Text)) dialog.ShowDialog();
            crashReport = null;
        }

        // Captured here (on the real UI thread, before the message loop even starts) so API handlers
        // running on Kestrel's thread pool can still marshal a native dialog (OpenFileDialog, ...) onto it.
        var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        var dialogs = new UiDialogService(ui);
        var windows = new UiWindowService(ui);

        var server = ServerApp.Build(args, dialogs, windows);
        HostText.Bind(server.Services.GetRequiredService<PreferencesStore>());
        if (crashReportError is not null)
            server.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CrashReport").LogWarning(crashReportError, "The crash report could not be built");
        // Before anything starts: if the app hangs while starting, this is the last line in the log.
        StartupDiagnostics.LogStarting(server.Services, args);
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

        // The plugins are loaded now; from here on a crash marker lists them.
        var pluginManager = server.Services.GetRequiredService<PluginManager>();
        CrashCapture.Plugins = () => pluginManager.TryGetPlugins()?.Select(p => new CrashPlugin(p.Id, p.Version, p.Status.ToString())).ToList();

        // What the data folder holds against what was loaded from it (see StartupDiagnostics).
        StartupDiagnostics.LogStarted(server.Services);

        // The installer starts the app again with this argument after an automatic update: say so once.
        var updateState = server.Services.GetRequiredService<UpdateStateStore>();
        var updated = StartupPolicy.WasUpdated(args, updateState.Get().LastRunVersion, ClientHub.ServerVersion);
        // Once per new version: a restore point of the data as the older version left it, before the new one touches anything.
        server.Services.GetRequiredService<BackupService>().TryCreateUpdatePoint(updateState.Get().LastRunVersion, ClientHub.ServerVersion);
        updateState.Update(s => s with { LastRunVersion = ClientHub.ServerVersion });
        // Downloaded installers never pile up: on every start only the newest one that is still newer than this version stays.
        server.Services.GetRequiredService<UpdateInstaller>().CleanUpDownloads();

        var openEditor = StartupPolicy.ShouldOpenEditor(args, server.Services.GetRequiredService<PreferencesStore>().Get());
        Application.Run(new TrayContext(server, openEditor, updated, crashReport, CrashReportPolicy.When(crashMarkers, openEditor)));

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
