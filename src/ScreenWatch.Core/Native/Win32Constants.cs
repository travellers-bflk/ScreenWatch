namespace ScreenWatch.Core.Native;

/// <summary>
/// Win32 API 常量定义，供 NativeMethods 和 WindowService 使用。
/// </summary>
public static class Win32Constants
{
    // ===== WinEvent 事件常量（user32.dll SetWinEventHook）=====

    /// <summary>前台窗口切换事件</summary>
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    /// <summary>窗口最小化恢复事件</summary>
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;

    // ===== WinEvent Hook 标志 =====

    /// <summary>事件回调在调用线程上下文外执行（不注入目标进程）</summary>
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    /// <summary>跳过本进程产生的事件</summary>
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    // ===== 进程访问权限（kernel32.dll OpenProcess）=====

    /// <summary>查询进程有限信息权限（不需要管理员权限即可获取 exe 路径）</summary>
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // ===== GetWindow 命令常量 =====

    /// <summary>获取窗口的所有者句柄</summary>
    public const uint GW_OWNER = 5;

    // ===== SHGetFileInfo 标志（shell32.dll）=====

    /// <summary>获取图标句柄。注意：Windows SDK 中 SHGFI_ICON = 0x100</summary>
    public const uint SHGFI_ICON = 0x00000100;

    /// <summary>获取大图标</summary>
    public const uint SHGFI_LARGEICON = 0x00000000;

    /// <summary>获取小图标</summary>
    public const uint SHGFI_SMALLICON = 0x00000001;
}
