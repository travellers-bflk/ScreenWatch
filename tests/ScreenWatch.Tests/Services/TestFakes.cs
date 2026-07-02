using ScreenWatch.Core.Native;
using ScreenWatch.Core.Services;

namespace ScreenWatch.Tests.Services;

/// <summary>
/// Fake <see cref="IWindowService"/> for unit tests. Allows controlling
/// foreground window, background windows, idle seconds, and icon bytes.
/// </summary>
public sealed class FakeWindowService : IWindowService
{
    /// <summary>The window returned by <see cref="GetForegroundWindowInfo"/>.</summary>
    public WindowInfo? ForegroundWindow { get; set; }

    /// <summary>Windows returned by <see cref="EnumerateVisibleTopLevelWindows"/>.</summary>
    public List<WindowInfo> BackgroundWindows { get; set; } = new();

    /// <summary>Value returned by <see cref="GetIdleSeconds"/>.</summary>
    public int IdleSecondsValue { get; set; }

    /// <summary>Value returned by <see cref="ExtractIconBytes"/>.</summary>
    public byte[]? IconBytes { get; set; }

    /// <summary>Records the callback passed to <see cref="HookForegroundChanged"/>.</summary>
    public WinEventProc? HookedCallback { get; private set; }

    public WindowInfo? GetForegroundWindowInfo() => ForegroundWindow;

    public WindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        var bg = BackgroundWindows.FirstOrDefault(w => w.Hwnd == hwnd);
        if (bg != null) return bg;
        if (ForegroundWindow?.Hwnd == hwnd) return ForegroundWindow;
        return null;
    }

    public List<WindowInfo> EnumerateVisibleTopLevelWindows(IntPtr? excludeHwnd = null)
    {
        if (!excludeHwnd.HasValue)
            return BackgroundWindows.ToList();

        return BackgroundWindows.Where(w => w.Hwnd != excludeHwnd.Value).ToList();
    }

    public int GetIdleSeconds() => IdleSecondsValue;

    public IntPtr HookForegroundChanged(WinEventProc callback)
    {
        HookedCallback = callback;
        return new IntPtr(1);
    }

    public void Unhook(IntPtr hHook) { /* no-op */ }

    public byte[]? ExtractIconBytes(string exePath) => IconBytes;
}

/// <summary>
/// Test double for <see cref="IIdleLockDetector"/>. Allows directly setting
/// <see cref="IsIdle"/>/<see cref="IsLocked"/> and raising events.
/// </summary>
public sealed class TestIdleLockDetector : IIdleLockDetector
{
    public bool IsIdle { get; set; }
    public bool IsLocked { get; set; }

    public event EventHandler? IdleStarted;
    public event EventHandler? IdleEnded;
    public event EventHandler? Locked;
    public event EventHandler? Unlocked;

    public void Start() { }
    public void Stop() { }

    public void RaiseIdleStarted()
    {
        IsIdle = true;
        IdleStarted?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseIdleEnded()
    {
        IsIdle = false;
        IdleEnded?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseLocked()
    {
        IsLocked = true;
        Locked?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseUnlocked()
    {
        IsLocked = false;
        Unlocked?.Invoke(this, EventArgs.Empty);
    }
}
