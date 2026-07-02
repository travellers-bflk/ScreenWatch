using ScreenWatch.Core.Native;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Abstracts the static <see cref="WindowService"/> so that business logic
/// (e.g. <see cref="UsageTrackingService"/>) can be unit-tested with fakes.
/// </summary>
public interface IWindowService
{
    /// <summary>Gets the current foreground window info, or null if none.</summary>
    WindowInfo? GetForegroundWindowInfo();

    /// <summary>Gets window info for the specified handle.</summary>
    WindowInfo? GetWindowInfo(IntPtr hwnd);

    /// <summary>Enumerates all visible, non-minimized top-level windows.</summary>
    List<WindowInfo> EnumerateVisibleTopLevelWindows(IntPtr? excludeHwnd = null);

    /// <summary>Returns the number of seconds since the last user input.</summary>
    int GetIdleSeconds();

    /// <summary>Registers a foreground-changed event hook and returns the hook handle.</summary>
    IntPtr HookForegroundChanged(WinEventProc callback);

    /// <summary>Removes a previously registered event hook.</summary>
    void Unhook(IntPtr hHook);

    /// <summary>Extracts the icon for the given exe path as PNG bytes.</summary>
    byte[]? ExtractIconBytes(string exePath);
}
