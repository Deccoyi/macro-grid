using System.Diagnostics;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Sessions;

namespace MacroGrid.Host;

/// <summary>
/// Leaves a crash marker when the process dies from an unhandled exception (see <see cref="CrashMarkers"/>). The handler runs while the process
/// is dying, so it stays small: no dependency injection, no texts, no preferences, no logger, no window. It writes one file synchronously and
/// returns; the runtime ends the process as it did before. The next start turns the marker into the crash report.
/// </summary>
internal static class CrashCapture
{
    private static readonly Stopwatch Uptime = Stopwatch.StartNew();
    private static string _crashesDir = "";
    private static string _logsDir = "";
    private static int _handled;
    private static volatile bool _running;

    /// <summary>
    /// The loaded plugins at the moment of the crash; set once the plugins are loaded, so a crash before that records "not known yet". It must not
    /// block: it returns null when the plugin list is busy, and the marker then says "not known" too.
    /// </summary>
    public static Func<IReadOnlyList<CrashPlugin>?>? Plugins { get; set; }

    /// <summary>Call first thing in <c>Main</c>. Exceptions on the UI thread are routed to the same handler as every other crash.</summary>
    public static void Install(string dataDir)
    {
        _crashesDir = Path.Combine(dataDir, CrashMarkers.FolderName);
        _logsDir = Path.Combine(dataDir, "logs");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
#if DEBUG
        if (Environment.GetEnvironmentVariable("MACROGRID_TEST_CRASH") == "starting") throw TestException();
#endif
    }

    /// <summary>The tray icon exists: a crash from now on is a "running" crash.</summary>
    public static void MarkRunning()
    {
        _running = true;
#if DEBUG
        // A harmless way to see the whole path: MACROGRID_TEST_CRASH=running (background thread) or =ui (UI thread), 5 seconds after the tray icon appears.
        switch (Environment.GetEnvironmentVariable("MACROGRID_TEST_CRASH"))
        {
            case "running":
                new Thread(() =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                    throw TestException();
                }) { IsBackground = true }.Start();
                break;
            case "ui":
                var timer = new System.Windows.Forms.Timer { Interval = 5000 };
                timer.Tick += (_, _) => throw TestException();
                timer.Start();
                break;
        }
#endif
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Only the first crash writes; a second one on another thread returns at once.
        if (Interlocked.Exchange(ref _handled, 1) != 0) return;
        try
        {
            var ex = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString());
            IReadOnlyList<CrashPlugin>? plugins = null;
            try { plugins = Plugins?.Invoke(); }
            catch (Exception) { /* the marker then says "not known" */ }

            var marker = CrashMarker.From(ex, _running ? CrashPhase.Running : CrashPhase.Starting, DateTimeOffset.Now, ClientHub.ServerVersion, Uptime.Elapsed, plugins, _logsDir)
                with { IsTerminating = e.IsTerminating };
            CrashMarkers.TryWrite(_crashesDir, marker);
        }
        catch (Exception)
        {
            // Never make a dying process worse.
        }
    }

#if DEBUG
    private static Exception TestException() => new InvalidOperationException(
        $"Test crash by {Environment.UserName} on {Environment.MachineName} while opening D:\\Private\\test.txt");
#endif
}
