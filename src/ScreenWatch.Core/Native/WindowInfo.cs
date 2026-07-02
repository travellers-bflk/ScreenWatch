namespace ScreenWatch.Core.Native;

/// <summary>
/// 窗口信息数据载体，封装从 Win32 API 获取的窗口相关数据。
/// </summary>
public class WindowInfo
{
    /// <summary>窗口句柄</summary>
    public IntPtr Hwnd { get; set; }

    /// <summary>窗口标题（业务层决定是否存储）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>窗口类名</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>进程 ID</summary>
    public uint ProcessId { get; set; }

    /// <summary>进程名（如 chrome.exe）</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>exe 完整路径（高权限进程可能获取不到，留空）</summary>
    public string ExePath { get; set; } = string.Empty;
}
