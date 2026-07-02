using ScreenWatch.App.Services;

namespace ScreenWatch.Tests;

/// <summary>
/// AutoStartService 单元测试。
/// 注册表操作涉及真实系统状态，测试设计为只读取不修改，
/// 或使用测试专用注册表路径进行隔离。
/// </summary>
public class AutoStartServiceTests
{
    /// <summary>
    /// 验证 IsAutoStartEnabled 在正常情况下不抛出异常。
    /// 此测试只读取注册表，不修改任何数据。
    /// </summary>
    [Fact]
    public void IsAutoStartEnabled_DoesNotThrow()
    {
        var service = new AutoStartService();

        var exception = Record.Exception(() => service.IsAutoStartEnabled());

        Assert.Null(exception);
    }

    /// <summary>
    /// 使用测试专用注册表路径验证 Enable/Disable 往返逻辑。
    /// 标记为 Skip 以避免在 CI 环境中修改注册表。
    /// 在本地真实 Windows 环境下可移除 Skip 特性手动运行验证。
    /// </summary>
    [Fact(Skip = "需真实 Windows 环境，避免在 CI 中修改注册表")]
    public void EnableAndDisableAutoStart_RoundTrip_IntegrationTest()
    {
        // 使用测试专用路径，避免影响真实的 Run 键
        const string testKeyPath = @"Software\ScreenWatch\TestRun";
        const string testName = "TestEntry";
        var service = new AutoStartService(testKeyPath, testName);

        try
        {
            // 确保初始状态为禁用
            service.DisableAutoStart();
            Assert.False(service.IsAutoStartEnabled());

            // 启用后应检测到
            service.EnableAutoStart();
            Assert.True(service.IsAutoStartEnabled());

            // 再次禁用后应检测不到
            service.DisableAutoStart();
            Assert.False(service.IsAutoStartEnabled());
        }
        finally
        {
            // 清理：确保测试注册表项被删除
            service.DisableAutoStart();
        }
    }
}
