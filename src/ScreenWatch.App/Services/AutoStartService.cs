using Microsoft.Win32;

namespace ScreenWatch.App.Services;

/// <summary>
/// 管理开机自启动功能，通过读写注册表
/// HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run 实现。
/// </summary>
public class AutoStartService
{
    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DefaultAppName = "ScreenWatch";
    private const string MinimizedArg = "--minimized";

    private readonly string _runKeyPath;
    private readonly string _appName;

    /// <summary>
    /// 使用默认注册表路径创建实例。
    /// </summary>
    public AutoStartService() : this(DefaultRunKeyPath, DefaultAppName)
    {
    }

    /// <summary>
    /// 使用指定的注册表路径和键名创建实例（可用于测试隔离）。
    /// </summary>
    /// <param name="runKeyPath">注册表子键路径（相对于 HKCU）。</param>
    /// <param name="appName">注册表值名称。</param>
    public AutoStartService(string runKeyPath, string appName)
    {
        _runKeyPath = runKeyPath;
        _appName = appName;
    }

    /// <summary>
    /// 检查开机自启动是否已启用。
    /// </summary>
    /// <returns>若注册表中存在对应键则返回 true，否则返回 false。</returns>
    public bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        if (key == null)
            return false;
        return key.GetValue(_appName) != null;
    }

    /// <summary>
    /// 启用开机自启动，将当前 exe 路径（附带 --minimized 参数）写入注册表 Run 键。
    /// </summary>
    public void EnableAutoStart()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);
        var exePath = Environment.ProcessPath;
        if (exePath == null)
            return;
        key.SetValue(_appName, $"\"{exePath}\" {MinimizedArg}");
    }

    /// <summary>
    /// 禁用开机自启动，从注册表 Run 键中删除对应项。
    /// </summary>
    public void DisableAutoStart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
        if (key == null)
            return;
        key.DeleteValue(_appName, throwOnMissingValue: false);
    }
}
