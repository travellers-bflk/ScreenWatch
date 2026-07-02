using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Tests.Data;

public class AppRepositoryTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly AppRepository _repo;

    public AppRepositoryTests()
    {
        _testDb = new TestDatabase();
        _repo = new AppRepository(_testDb.Db);
    }

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public void GetOrCreateApp_CreatesNewApp_ReturnsWithId()
    {
        var app = _repo.GetOrCreateApp(@"C:\Program Files\test\app.exe", "app.exe", "Test App");

        Assert.NotNull(app);
        Assert.True(app!.AppId > 0);
        Assert.Equal(@"C:\Program Files\test\app.exe", app.ExePath);
        Assert.Equal("app.exe", app.ExeName);
        Assert.Equal("Test App", app.DisplayName);
        Assert.False(app.IsRecognized);
    }

    [Fact]
    public void GetOrCreateApp_SameExePath_ReturnsSameAppId()
    {
        var app1 = _repo.GetOrCreateApp(@"C:\test\app.exe", "app.exe");
        var app2 = _repo.GetOrCreateApp(@"C:\test\app.exe", "app.exe");

        Assert.NotNull(app1);
        Assert.NotNull(app2);
        Assert.Equal(app1!.AppId, app2!.AppId);
    }

    [Fact]
    public void GetOrCreateApp_DifferentExePath_ReturnsDifferentAppId()
    {
        var app1 = _repo.GetOrCreateApp(@"C:\test\app1.exe", "app1.exe");
        var app2 = _repo.GetOrCreateApp(@"C:\test\app2.exe", "app2.exe");

        Assert.NotNull(app1);
        Assert.NotNull(app2);
        Assert.NotEqual(app1!.AppId, app2!.AppId);
    }

    [Fact]
    public void GetOrCreateApp_NullDisplayName_StoresNullAndReturnsEmpty()
    {
        var app = _repo.GetOrCreateApp(@"C:\test\noname.exe", "noname.exe");

        Assert.NotNull(app);
        Assert.Equal(string.Empty, app!.DisplayName);
    }

    [Fact]
    public void GetById_ReturnsCorrectApp()
    {
        var created = _repo.GetOrCreateApp(@"C:\test\findme.exe", "findme.exe", "Find Me");
        var found = _repo.GetById(created!.AppId);

        Assert.NotNull(found);
        Assert.Equal(created.AppId, found!.AppId);
        Assert.Equal("findme.exe", found.ExeName);
        Assert.Equal("Find Me", found.DisplayName);
    }

    [Fact]
    public void GetById_Nonexistent_ReturnsNull()
    {
        var result = _repo.GetById(99999);
        Assert.Null(result);
    }

    [Fact]
    public void UpdateCategory_SetsCategoryAndMarksRecognized()
    {
        var app = _repo.GetOrCreateApp(@"C:\test\categorize.exe", "categorize.exe");
        _repo.UpdateCategory(app!.AppId, categoryId: 3);

        var updated = _repo.GetById(app.AppId);
        Assert.NotNull(updated);
        Assert.Equal(3, updated!.CategoryId);
        Assert.True(updated.IsRecognized);
    }

    [Fact]
    public void UpdateDisplayName_UpdatesName()
    {
        var app = _repo.GetOrCreateApp(@"C:\test\rename.exe", "rename.exe", "Old Name");
        _repo.UpdateDisplayName(app!.AppId, "New Name");

        var updated = _repo.GetById(app.AppId);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated!.DisplayName);
    }

    [Fact]
    public void GetUnrecognizedApps_ReturnsOnlyUnrecognized()
    {
        var app1 = _repo.GetOrCreateApp(@"C:\test\unrec1.exe", "unrec1.exe");
        var app2 = _repo.GetOrCreateApp(@"C:\test\unrec2.exe", "unrec2.exe");
        var app3 = _repo.GetOrCreateApp(@"C:\test\rec1.exe", "rec1.exe");
        _repo.UpdateCategory(app3!.AppId, 1);

        var unrecognized = _repo.GetUnrecognizedApps();

        Assert.Equal(2, unrecognized.Count);
        Assert.DoesNotContain(unrecognized, a => a.AppId == app3!.AppId);
        Assert.Contains(unrecognized, a => a.AppId == app1!.AppId);
        Assert.Contains(unrecognized, a => a.AppId == app2!.AppId);
    }

    [Fact]
    public void GetAllApps_ReturnsAllApps()
    {
        _repo.GetOrCreateApp(@"C:\test\a1.exe", "a1.exe", "App C");
        _repo.GetOrCreateApp(@"C:\test\a2.exe", "a2.exe", "App A");
        _repo.GetOrCreateApp(@"C:\test\a3.exe", "a3.exe", "App B");

        var all = _repo.GetAllApps();
        Assert.Equal(3, all.Count);
        // Ordered by display_name
        Assert.Equal("App A", all[0].DisplayName);
        Assert.Equal("App B", all[1].DisplayName);
        Assert.Equal("App C", all[2].DisplayName);
    }
}
