using ScreenWatch.Core.Native;

namespace ScreenWatch.Tests.Native;

/// <summary>
/// WindowService 集成测试（依赖真实系统状态）。
/// 验证方法不抛异常且返回值类型正确。
/// </summary>
public class WindowServiceTests
{
    [Fact]
    public void GetForegroundWindowInfo_DoesNotThrow()
    {
        // Act — 在测试运行环境中应有一个前台窗口（如 IDE / 终端）
        var result = WindowService.GetForegroundWindowInfo();

        // Assert — 返回 null（无前台窗口）或有效的 WindowInfo
        if (result is not null)
        {
            Assert.NotEqual(IntPtr.Zero, result.Hwnd);
            Assert.NotNull(result.Title);
            Assert.NotNull(result.ClassName);
            Assert.NotNull(result.ProcessName);
            Assert.NotNull(result.ExePath);
        }
    }

    [Fact]
    public void EnumerateVisibleTopLevelWindows_DoesNotThrowAndReturnsList()
    {
        // Act
        var windows = WindowService.EnumerateVisibleTopLevelWindows();

        // Assert — 返回有效列表（运行环境中通常有可见窗口）
        Assert.NotNull(windows);
        // 验证列表中每个元素的属性已填充
        foreach (var w in windows)
        {
            Assert.NotEqual(IntPtr.Zero, w.Hwnd);
            Assert.NotNull(w.Title);
            Assert.NotNull(w.ClassName);
            Assert.NotNull(w.ProcessName);
        }
    }

    [Fact]
    public void EnumerateVisibleTopLevelWindows_WithExcludeHwnd_ExcludesWindow()
    {
        // Arrange — 获取前台窗口用于排除
        var fg = WindowService.GetForegroundWindowInfo();
        IntPtr excludeHwnd = fg?.Hwnd ?? IntPtr.Zero;

        // Act
        var windows = WindowService.EnumerateVisibleTopLevelWindows(excludeHwnd);

        // Assert — 排除的窗口不在列表中
        Assert.NotNull(windows);
        if (excludeHwnd != IntPtr.Zero)
        {
            Assert.DoesNotContain(windows, w => w.Hwnd == excludeHwnd);
        }
    }

    [Fact]
    public void GetIdleSeconds_ReturnsNonNegativeValue()
    {
        // Act
        int idleSeconds = WindowService.GetIdleSeconds();

        // Assert — 空闲秒数应非负
        Assert.True(idleSeconds >= 0);
    }

    [Fact]
    public void ExtractIconBytes_ForKnownExe_ReturnsPngBytes()
    {
        // Arrange — 使用 Windows 系统自带的 notepad.exe
        string notepadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "notepad.exe");

        // 如果 notepad 不存在则跳过（极罕见的 Windows 配置）
        if (!File.Exists(notepadPath))
        {
            return; // Skip
        }

        // Act
        var iconBytes = WindowService.ExtractIconBytes(notepadPath);

        // Assert — 应返回非空的 PNG 字节数组
        Assert.NotNull(iconBytes);
        Assert.True(iconBytes.Length > 0);
        // PNG 文件头魔数：89 50 4E 47 0D 0A 1A 0A
        Assert.Equal(0x89, iconBytes[0]);
        Assert.Equal(0x50, iconBytes[1]);
        Assert.Equal(0x4E, iconBytes[2]);
        Assert.Equal(0x47, iconBytes[3]);
    }

    [Fact]
    public void ExtractIconBytes_ForNonExistentPath_ReturnsNull()
    {
        // Act
        var iconBytes = WindowService.ExtractIconBytes("C:\\nonexistent\\fake.exe");

        // Assert — 不存在的路径应返回 null
        Assert.Null(iconBytes);
    }

    [Fact]
    public void ExtractIconBytes_ForEmptyPath_ReturnsNull()
    {
        // Act
        var iconBytes = WindowService.ExtractIconBytes("");

        // Assert
        Assert.Null(iconBytes);
    }
}
