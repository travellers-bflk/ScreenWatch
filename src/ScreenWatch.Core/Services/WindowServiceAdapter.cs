using ScreenWatch.Core.Native;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Production implementation of <see cref="IWindowService"/> that delegates
/// every call to the static <see cref="WindowService"/>.
/// </summary>
public sealed class WindowServiceAdapter : IWindowService
{
    /// <inheritdoc />
    public WindowInfo? GetForegroundWindowInfo() => WindowService.GetForegroundWindowInfo();

    /// <inheritdoc />
    public WindowInfo? GetWindowInfo(IntPtr hwnd) => WindowService.GetWindowInfo(hwnd);

    /// <inheritdoc />
    public List<WindowInfo> EnumerateVisibleTopLevelWindows(IntPtr? excludeHwnd = null)
        => WindowService.EnumerateVisibleTopLevelWindows(excludeHwnd);

    /// <inheritdoc />
    public int GetIdleSeconds() => WindowService.GetIdleSeconds();

    /// <inheritdoc />
    public IntPtr HookForegroundChanged(WinEventProc callback)
        => WindowService.HookForegroundChanged(callback);

    /// <inheritdoc />
    public void Unhook(IntPtr hHook) => WindowService.Unhook(hHook);

    /// <inheritdoc />
    public byte[]? ExtractIconBytes(string exePath) => WindowService.ExtractIconBytes(exePath);
}
