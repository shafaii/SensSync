using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SensSync.Core;

namespace SensSync.Desktop;

/// <summary>Normal process names and foreground PID only. No process memory, input hooks, or injection.</summary>
internal sealed class WindowsGameMonitor : IDisposable
{
    private readonly GameDetectionTracker tracker = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    private readonly WinEventCallback callback;
    private readonly nint hook;
    private bool disposed;
    public event Action<GameDetectionState>? Changed;

    public WindowsGameMonitor()
    {
        callback = OnForeground;
        // OUTOFCONTEXT (0) | SKIPOWNPROCESS (2). Root callback for the hook lifetime.
        hook = SetWinEventHook(3, 3, 0, callback, 0, 0, 2);
        timer.Tick += (_, _) => Poll();
        timer.Start();
    }

    public void Poll()
    {
        if (disposed) return;
        var observed = new List<ObservedProcess>();
        bool available = true;
        foreach (string executableName in GameDetectionTracker.ExecutableNames)
        {
            try
            {
                var matches = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executableName));
                try
                {
                    foreach (var process in matches) observed.Add(new(process.Id, executableName));
                }
                finally { foreach (var process in matches) process.Dispose(); }
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
            { available = false; }
        }
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        Changed?.Invoke(tracker.Observe(observed, unchecked((int)pid), available));
    }

    private void OnForeground(nint hookHandle, uint eventType, nint window, int objectId, int childId, uint threadId, uint time)
        => Poll();

    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        timer.Dispose();
        if (hook != 0) UnhookWinEvent(hook);
        GC.KeepAlive(callback);
    }

    private delegate void WinEventCallback(nint hook, uint eventType, nint window, int objectId, int childId, uint threadId, uint time);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventCallback callback, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(nint hook);
}
