using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenWatch.Core.Native;

/// <summary>
/// 类型安全的 Win32 API 高层封装，供后续业务模块（采集核心、计时器等）直接调用。
/// 所有方法均做了异常防护与降级处理，确保在权限不足等场景下不会抛异常。
/// </summary>
public static class WindowService
{
    /// <summary>
    /// 控制是否采集窗口标题。默认 false（不采集），由 UsageTrackingService.Start 从设置读取后赋值。
    /// 设为 false 时 GetWindowInfo 跳过 GetWindowText 调用，Title 返回空字符串。
    /// </summary>
    public static bool CaptureWindowTitle { get; set; } = false;

    /// <summary>
    /// 获取当前前台窗口的完整信息。
    /// </summary>
    /// <returns>前台窗口信息；若无前台窗口（如锁屏）返回 null</returns>
    public static WindowInfo? GetForegroundWindowInfo()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        return GetWindowInfo(hwnd);
    }

    /// <summary>
    /// 获取指定窗口的信息（标题、类名、PID、进程名、exe 路径）。
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>窗口信息；若 hwnd 为 Zero 返回 null</returns>
    public static WindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        var info = new WindowInfo { Hwnd = hwnd };

        // 窗口标题（受 CaptureWindowTitle 静态属性控制，默认不采集以保护隐私）
        string title = string.Empty;
        if (CaptureWindowTitle)
        {
            var titleBuilder = new StringBuilder(256);
            NativeMethods.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            title = titleBuilder.ToString();
        }
        info.Title = title;

        // 窗口类名
        var classBuilder = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, classBuilder, classBuilder.Capacity);
        info.ClassName = classBuilder.ToString();

        // 进程 ID
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        info.ProcessId = pid;

        // 进程名与 exe 路径（含降级逻辑）
        var (processName, exePath) = GetProcessInfo(pid);
        info.ProcessName = processName;
        info.ExePath = exePath;

        return info;
    }

    /// <summary>
    /// 枚举所有可见的顶层窗口（非最小化、无 owner）。
    /// </summary>
    /// <param name="excludeHwnd">需排除的窗口句柄（通常为前台窗口），可为 null</param>
    /// <returns>可见顶层窗口列表</returns>
    public static List<WindowInfo> EnumerateVisibleTopLevelWindows(IntPtr? excludeHwnd = null)
    {
        var windows = new List<WindowInfo>();

        // 使用局部变量保持委托引用，防止 GC 回收
        EnumWindowsProc callback = (hWnd, lParam) =>
        {
            // 排除指定窗口
            if (excludeHwnd.HasValue && hWnd == excludeHwnd.Value)
                return true;

            // 过滤：必须可见
            if (!NativeMethods.IsWindowVisible(hWnd))
                return true;

            // 过滤：排除最小化窗口
            if (NativeMethods.IsIconic(hWnd))
                return true;

            // 过滤：只保留无 owner 的顶层窗口（排除对话框、子窗口等）
            IntPtr owner = NativeMethods.GetWindow(hWnd, Win32Constants.GW_OWNER);
            if (owner != IntPtr.Zero)
                return true;

            // 收集窗口信息
            var info = GetWindowInfo(hWnd);
            if (info != null)
                windows.Add(info);

            return true; // 继续枚举
        };

        NativeMethods.EnumWindows(callback, IntPtr.Zero);
        return windows;
    }

    /// <summary>
    /// 计算用户空闲秒数（自最后一次键盘/鼠标输入以来）。
    /// </summary>
    /// <returns>空闲秒数（非负整数；获取失败返回 0）</returns>
    public static int GetIdleSeconds()
    {
        var lii = new LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>();

        if (!NativeMethods.GetLastInputInfo(ref lii))
            return 0;

        // GetTickCount64 返回 64 位毫秒计数（不会溢出）
        // LASTINPUTINFO.dwTime 是 32 位 GetTickCount 值（每 ~49.7 天回绕一次）
        // 将 GetTickCount64 的低 32 位与 dwTime 做无符号减法，
        // uint 减法自动处理回绕，得到正确的空闲毫秒数
        ulong now64 = NativeMethods.GetTickCount64();
        uint now32 = (uint)now64;
        uint idleMs = now32 - lii.dwTime;

        return (int)(idleMs / 1000);
    }

    /// <summary>
    /// 注册前台窗口切换事件钩子（EVENT_SYSTEM_FOREGROUND ～ EVENT_SYSTEM_MINIMIZEEND）。
    /// </summary>
    /// <param name="callback">事件回调委托（调用方必须保持委托引用存活，防止 GC 回收）</param>
    /// <returns>钩子句柄；失败返回 IntPtr.Zero</returns>
    public static IntPtr HookForegroundChanged(WinEventProc callback)
    {
        uint flags = Win32Constants.WINEVENT_OUTOFCONTEXT | Win32Constants.WINEVENT_SKIPOWNPROCESS;
        return NativeMethods.SetWinEventHook(
            Win32Constants.EVENT_SYSTEM_FOREGROUND,
            Win32Constants.EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero,
            callback,
            0,
            0,
            flags);
    }

    /// <summary>
    /// 移除事件钩子。
    /// </summary>
    /// <param name="hHook">HookForegroundChanged 返回的钩子句柄</param>
    public static void Unhook(IntPtr hHook)
    {
        if (hHook != IntPtr.Zero)
            NativeMethods.UnhookWinEvent(hHook);
    }

    /// <summary>
    /// 从 exe 路径提取图标，转为 PNG 字节数组（供缓存使用）。
    /// 使用 SHGetFileInfo 获取 HICON，转 Bitmap 后输出 PNG。
    /// </summary>
    /// <param name="exePath">exe 完整路径</param>
    /// <returns>PNG 字节数组；提取失败返回 null</returns>
    public static byte[]? ExtractIconBytes(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return null;

        var shfi = new SHFILEINFO();
        uint flags = Win32Constants.SHGFI_ICON | Win32Constants.SHGFI_LARGEICON;
        IntPtr hResult = NativeMethods.SHGetFileInfo(
            exePath,
            0,
            ref shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            flags);

        if (hResult == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
            return null;

        try
        {
            // Icon.FromHandle 不接管 HICON 所有权，需手动 DestroyIcon
            using var icon = Icon.FromHandle(shfi.hIcon);
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            // 图标转换失败（如 HICON 无效），返回 null
            return null;
        }
        finally
        {
            // 必须 DestroyIcon 释放 HICON，防止 GDI 句柄泄漏
            NativeMethods.DestroyIcon(shfi.hIcon);
        }
    }

    /// <summary>
    /// 获取进程名和 exe 完整路径。
    /// 优先用 OpenProcess + QueryFullProcessImageName（低权限友好），
    /// 失败时降级为 Process.GetProcessById 获取进程名（不含 exe 路径）。
    /// 绝不使用 Process.MainModule.FileName（对高权限进程会抛异常）。
    /// </summary>
    private static (string processName, string exePath) GetProcessInfo(uint pid)
    {
        string processName = string.Empty;
        string exePath = string.Empty;

        IntPtr hProcess = IntPtr.Zero;
        try
        {
            hProcess = NativeMethods.OpenProcess(
                Win32Constants.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                pid);

            if (hProcess != IntPtr.Zero)
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                {
                    exePath = sb.ToString();
                    processName = Path.GetFileName(exePath);
                }
            }
        }
        catch
        {
            // P/Invoke 调用异常，静默处理，进入降级路径
        }
        finally
        {
            if (hProcess != IntPtr.Zero)
                NativeMethods.CloseHandle(hProcess);
        }

        // 降级：通过 Process.GetProcessById 获取进程名（不含路径）
        if (string.IsNullOrEmpty(processName))
        {
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                // ProcessName 不含 .exe 后缀，需手动补上
                processName = proc.ProcessName + ".exe";
            }
            catch
            {
                // 进程可能已退出或访问被拒，留空
            }
        }

        return (processName, exePath);
    }
}
