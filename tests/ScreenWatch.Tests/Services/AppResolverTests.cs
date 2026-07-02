using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;
using ScreenWatch.Core.Native;
using ScreenWatch.Core.Services;
using ScreenWatch.Tests.Data;

namespace ScreenWatch.Tests.Services;

public class AppResolverTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly AppRepository _appRepo;
    private readonly AppResolver _resolver;
    private readonly FakeWindowService _fakeWindowService;

    public AppResolverTests()
    {
        _testDb = new TestDatabase();
        _appRepo = new AppRepository(_testDb.Db);
        _fakeWindowService = new FakeWindowService();
        _resolver = new AppResolver(_appRepo, _fakeWindowService, iconDir: Path.GetTempPath());
    }

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public void ResolveApp_SameExePath_ReturnsSameAppId()
    {
        var window1 = new WindowInfo { ExePath = @"C:\test\app.exe", ProcessName = "app.exe" };
        var window2 = new WindowInfo { ExePath = @"C:\test\app.exe", ProcessName = "app.exe" };

        var app1 = _resolver.ResolveApp(window1);
        var app2 = _resolver.ResolveApp(window2);

        Assert.NotNull(app1);
        Assert.NotNull(app2);
        Assert.Equal(app1!.AppId, app2!.AppId);
    }

    [Fact]
    public void ResolveApp_DifferentExePath_ReturnsDifferentAppId()
    {
        var window1 = new WindowInfo { ExePath = @"C:\test\app1.exe", ProcessName = "app1.exe" };
        var window2 = new WindowInfo { ExePath = @"C:\test\app2.exe", ProcessName = "app2.exe" };

        var app1 = _resolver.ResolveApp(window1);
        var app2 = _resolver.ResolveApp(window2);

        Assert.NotNull(app1);
        Assert.NotNull(app2);
        Assert.NotEqual(app1!.AppId, app2!.AppId);
    }

    [Fact]
    public void ResolveApp_EmptyExePath_UsesProcessNameAsFallback()
    {
        var window = new WindowInfo { ExePath = "", ProcessName = "chrome.exe" };

        var app = _resolver.ResolveApp(window);

        Assert.NotNull(app);
        // ProcessName used as exePath when ExePath is empty
        Assert.Equal("chrome.exe", app!.ExePath);
        Assert.Equal("chrome.exe", app.ExeName);
        // DisplayName should be ProcessName without .exe
        Assert.Equal("chrome", app.DisplayName);
    }

    [Fact]
    public void ResolveApp_EmptyExePath_DeducesByProcessName()
    {
        var window1 = new WindowInfo { ExePath = "", ProcessName = "chrome.exe" };
        var window2 = new WindowInfo { ExePath = "", ProcessName = "chrome.exe" };

        var app1 = _resolver.ResolveApp(window1);
        var app2 = _resolver.ResolveApp(window2);

        Assert.NotNull(app1);
        Assert.NotNull(app2);
        // Same ProcessName fallback key should produce same AppId
        Assert.Equal(app1!.AppId, app2!.AppId);
    }

    [Fact]
    public void ResolveApp_BothEmpty_ReturnsNull()
    {
        var window = new WindowInfo { ExePath = "", ProcessName = "" };

        var app = _resolver.ResolveApp(window);

        Assert.Null(app);
    }

    [Fact]
    public void ResolveApp_PersistsAndRetrievesCorrectFields()
    {
        var window = new WindowInfo
        {
            ExePath = @"C:\Program Files\MyApp\myapp.exe",
            ProcessName = "myapp.exe"
        };

        var app = _resolver.ResolveApp(window);

        Assert.NotNull(app);
        Assert.True(app!.AppId > 0);
        Assert.Equal(@"C:\Program Files\MyApp\myapp.exe", app.ExePath);
        Assert.Equal("myapp.exe", app.ExeName);
        Assert.Equal("myapp", app.DisplayName);
        Assert.False(app.IsRecognized);

        // Verify it was persisted to the database
        var fromDb = _appRepo.GetById(app.AppId);
        Assert.NotNull(fromDb);
        Assert.Equal(app.AppId, fromDb!.AppId);
    }

    [Fact]
    public void GetIcon_CachesResultInMemory()
    {
        _fakeWindowService.IconBytes = new byte[] { 1, 2, 3 };

        var first = _resolver.GetIcon(@"C:\test\app.exe");
        var second = _resolver.GetIcon(@"C:\test\app.exe");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetIcon_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(_resolver.GetIcon(""));
        Assert.Null(_resolver.GetIcon(null!));
    }
}
