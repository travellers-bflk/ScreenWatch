using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenWatch.App.Services;

/// <summary>
/// 使用 HwndSource（WPF消息窗口）+ 原生 Shell_NotifyIconW 创建系统托盘图标。
/// 不依赖任何第三方库，直接调用 Win32 API，兼容 .NET 9。
/// </summary>
public class WpfTrayService : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_LEFTALIGN = 0x00000000;
    private const uint TPM_RETURNCMD = 0x00000100;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hwnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private IntPtr _hIcon;
    private Icon? _icon;
    private bool _disposed;
    private readonly string _tooltip = "ScreenWatch - 屏幕使用时间统计";

    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WpfTrayService));

        // 使用 HwndSource 创建消息专用窗口（融入 WPF 消息循环）
        var parameters = new HwndSourceParameters("ScreenWatchTray")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            PositionX = 0,
            PositionY = 0,
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
        _hwnd = _hwndSource.Handle;

        // 加载应用图标
        LoadAppIcon();

        // 添加托盘图标
        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = _tooltip
        };

        if (!Shell_NotifyIconW(NIM_ADD, ref nid))
            throw new InvalidOperationException("Shell_NotifyIconW(NIM_ADD) failed");
    }

    private void LoadAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null && File.Exists(exePath))
            {
                _icon = Icon.ExtractAssociatedIcon(exePath);
                if (_icon != null)
                {
                    _hIcon = _icon.Handle;
                    return;
                }
            }
        }
        catch { }

        _hIcon = SystemIcons.Application.Handle;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMessage = lParam.ToInt32() & 0xFFFF;

            if (mouseMessage == WM_LBUTTONDBLCLK)
            {
                ShowWindowRequested?.Invoke();
                handled = true;
            }
            else if (mouseMessage == WM_RBUTTONUP)
            {
                ShowContextMenu();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        AppendMenuW(hMenu, MF_STRING, 1, "显示主窗口");
        AppendMenuW(hMenu, MF_SEPARATOR, 0, null);
        AppendMenuW(hMenu, MF_STRING, 2, "退出");

        GetCursorPos(out POINT pt);
        SetForegroundWindow(_hwnd);

        int cmd = TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_RETURNCMD, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

        DestroyMenu(hMenu);

        if (cmd == 1)
            ShowWindowRequested?.Invoke();
        else if (cmd == 2)
            ExitRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1
        };
        Shell_NotifyIconW(NIM_DELETE, ref nid);

        _hwndSource?.Dispose();
        _hwndSource = null;

        _disposed = true;
    }
}
