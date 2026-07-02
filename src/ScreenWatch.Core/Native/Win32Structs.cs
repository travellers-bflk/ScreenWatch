using System.Runtime.InteropServices;

namespace ScreenWatch.Core.Native;

/// <summary>
/// GetLastInputInfo 所用的结构体，记录最后一次输入时间。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LASTINPUTINFO
{
    /// <summary>结构体大小（字节），调用前必须设置</summary>
    public uint cbSize;

    /// <summary>最后一次输入事件对应的 GetTickCount 值（毫秒）</summary>
    public uint dwTime;
}

/// <summary>
/// SHGetFileInfo 所用的结构体，包含文件/图标信息。
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct SHFILEINFO
{
    /// <summary>图标句柄（HICON）</summary>
    public IntPtr hIcon;

    /// <summary>系统图标列表中的索引</summary>
    public int iIcon;

    /// <summary>文件属性</summary>
    public uint dwAttributes;

    /// <summary>显示名（MAX_PATH = 260）</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szDisplayName;

    /// <summary>类型名（80 字符）</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string szTypeName;
}

/// <summary>
/// EnumWindows 回调委托。返回 true 继续枚举，false 停止。
/// </summary>
/// <param name="hWnd">窗口句柄</param>
/// <param name="lParam">调用方传递的自定义数据</param>
public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

/// <summary>
/// SetWinEventHook 事件回调委托。
/// </summary>
/// <param name="hWinEventHook">事件钩子句柄</param>
/// <param name="eventType">事件类型（如 EVENT_SYSTEM_FOREGROUND）</param>
/// <param name="hwnd">产生事件的窗口句柄</param>
/// <param name="idObject">对象 ID</param>
/// <param name="idChild">子对象 ID</param>
/// <param name="dwEventThread">产生事件的线程 ID</param>
/// <param name="dwmsEventTime">事件时间（GetTickCount 毫秒值）</param>
public delegate void WinEventProc(
    IntPtr hWinEventHook,
    uint eventType,
    IntPtr hwnd,
    int idObject,
    int idChild,
    uint dwEventThread,
    uint dwmsEventTime);
