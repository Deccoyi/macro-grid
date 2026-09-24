using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MacroStation.Core.Sessions;

namespace MacroStation.Windows.Windows;

/// <summary>
/// <see cref="IActiveWindowSource"/> via <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND)</c> — event-driven,
/// not polling, to keep the host's idle CPU footprint flat (see docs/design/auto-profile-switch.md). <c>WINEVENT_OUTOFCONTEXT</c> delivers the callback on
/// whichever thread called <c>SetWinEventHook</c>, so this owns a dedicated thread with its own native
/// message loop rather than piggy-backing on WPF/WinForms — this project has no UI framework dependency
/// otherwise and shouldn't gain one just for this.
/// </summary>
public sealed class ForegroundWindowMonitor : IActiveWindowSource
{
    public event Action<ForegroundWindow>? ForegroundChanged;

    // Kept alive as a field — a delegate passed to native code with nothing else referencing it is a
    // classic GC-collects-mid-callback bug once a hook fires after a Gen0 collection.
    private readonly WinEventDelegate _callback;
    private Thread? _thread;
    private bool _started;

    public ForegroundWindowMonitor()
    {
        _callback = OnWinEvent;
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        _thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "ForegroundWindowMonitor" };
        _thread.Start();
    }

    public bool HasVisibleWindow(string processName)
    {
        var found = false;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            if (!string.Equals(ProcessNameFor(hWnd), processName, StringComparison.OrdinalIgnoreCase)) return true;
            found = true;
            return false; // stop enumerating, we have our answer
        }, IntPtr.Zero);
        return found;
    }

    public IReadOnlyList<ForegroundWindow> ListVisibleWindows()
    {
        var seen = new Dictionary<string, ForegroundWindow>(StringComparer.OrdinalIgnoreCase);
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var title = TitleFor(hWnd);
            if (title.Length == 0) return true; // most invisible-but-technically-"visible" helper windows have no title
            var processName = ProcessNameFor(hWnd);
            if (processName is null || seen.ContainsKey(processName)) return true;
            seen[processName] = new ForegroundWindow(processName, title);
            return true;
        }, IntPtr.Zero);
        return [.. seen.Values.OrderBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)];
    }

    private void RunMessageLoop()
    {
        var hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _callback, 0, 0, WINEVENT_OUTOFCONTEXT);
        try
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            if (hook != IntPtr.Zero) UnhookWinEvent(hook);
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        var processName = ProcessNameFor(hwnd);
        if (processName is null) return;
        ForegroundChanged?.Invoke(new ForegroundWindow(processName, TitleFor(hwnd)));
    }

    private static string? ProcessNameFor(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == 0) return null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }
        catch (ArgumentException)
        {
            return null; // process exited between the event firing and us looking it up
        }
    }

    private static string TitleFor(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length <= 0) return "";
        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private const uint EVENT_SYSTEM_FOREGROUND = 3;
    private const uint WINEVENT_OUTOFCONTEXT = 0;

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
