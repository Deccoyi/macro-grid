using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Host;

/// <summary>
/// Writes why the process ends into today's log file. It appends synchronously (not through the queued file logger), because the
/// process may be dying and a queued line would be lost. Reasons are fixed texts or exception type and message: no tokens, PINs or paths.
/// </summary>
internal static class ShutdownLog
{
    private static string _logsDir = "";
    private static string? _requested;
    private static readonly List<PosixSignalRegistration> Signals = [];

    /// <summary>Call first thing in <c>Main</c>, with the logs folder.</summary>
    public static void Install(string dataDir)
    {
        _logsDir = Path.Combine(dataDir, "logs");
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            Write($"Process exiting ({_requested ?? "no exit was requested by the app: ended from outside"}), exit code {Environment.ExitCode}");

        // Console-control events (Ctrl+C, Ctrl+Break, console close) arrive as these signals. Cancel stays false: only a log line is added.
        foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM, PosixSignal.SIGHUP })
        {
            try { Signals.Add(PosixSignalRegistration.Create(signal, ctx => Write($"Received {signal} ({(int)ctx.Signal})"))); }
            catch (Exception) { /* a signal this host cannot observe is simply not logged */ }
        }

        // Logoff / Windows shutdown. Raised on the UI thread once the message loop runs; a failure to subscribe must not stop the app.
        try
        {
            SystemEvents.SessionEnding += (_, e) => Write($"Windows session ending: {e.Reason}");
            SystemEvents.SessionEnded += (_, e) => Write($"Windows session ended: {e.Reason}");
        }
        catch (Exception) { /* no message window available */ }
    }

    /// <summary>Remembers why the app itself asked to exit (first reason wins) and logs it.</summary>
    public static void Requested(string reason)
    {
        if (Interlocked.CompareExchange(ref _requested, reason, null) is null) Write($"Exit requested: {reason}");
    }

    public static void UnhandledException(object? exceptionObject, bool isTerminating)
    {
        var text = exceptionObject is Exception ex ? $"{ex.GetType().Name}: {ex.Message}" : "non-exception object";
        Write($"Unhandled exception (terminating: {isTerminating}): {text.ReplaceLineEndings(" ")}");
    }

    public static void Write(string message)
    {
        try
        {
            if (_logsDir.Length == 0) return;
            Directory.CreateDirectory(_logsDir);
            var path = Path.Combine(_logsDir, LogRetention.FileName(DateOnly.FromDateTime(DateTime.Now)));
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            var bytes = new UTF8Encoding(false).GetBytes($"{DateTime.Now:HH:mm:ss.fff} [INFO] Shutdown: {message}{Environment.NewLine}");
            stream.Write(bytes);
        }
        catch (Exception)
        {
            // Never make a dying process worse.
        }
    }
}
