using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;
using ScreenWatch.Core.Services;
using ScreenWatch.Tests.Data;

namespace ScreenWatch.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly CategoryRepository _categoryRepo;
    private readonly AppRepository _appRepo;
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _testDb = new TestDatabase();
        _categoryRepo = new CategoryRepository(_testDb.Db);
        _appRepo = new AppRepository(_testDb.Db);
        _service = new CategoryService(_categoryRepo, _appRepo, _testDb.Db);
    }

    public void Dispose() => _testDb.Dispose();

    // ====================================================================
    //  GetAllCategories
    // ====================================================================

    [Fact]
    public void GetAllCategories_ReturnsSeededDefaults()
    {
        var categories = _service.GetAllCategories();

        // V1 seeds 5 categories, V2 adds 4 more (开发, 工具, 媒体, 游戏)
        Assert.Equal(9, categories.Count);

        var names = categories.Select(c => c.Name).ToList();
        Assert.Contains("工作", names);
        Assert.Contains("娱乐", names);
        Assert.Contains("学习", names);
        Assert.Contains("社交", names);
        Assert.Contains("其他", names);
        Assert.Contains("开发", names);
        Assert.Contains("工具", names);
        Assert.Contains("媒体", names);
        Assert.Contains("游戏", names);
    }

    [Fact]
    public void GetAllCategories_ReturnsOrderedByName()
    {
        // The repository uses SQLite ORDER BY name (BINARY collation = ordinal byte order).
        // Verify the returned list matches ordinal sorting.
        var categories = _service.GetAllCategories();
        var sorted = categories.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, categories.Select(c => c.Name).ToList());
    }

    // ====================================================================
    //  GetCategory
    // ====================================================================

    [Fact]
    public void GetCategory_Existing_ReturnsCategory()
    {
        var all = _service.GetAllCategories();
        var workCategory = all.First(c => c.Name == "工作");

        var result = _service.GetCategory(workCategory.CategoryId);

        Assert.NotNull(result);
        Assert.Equal("工作", result!.Name);
        Assert.Equal("#4A90D9", result.Color);
    }

    [Fact]
    public void GetCategory_Nonexistent_ReturnsNull()
    {
        Assert.Null(_service.GetCategory(99999));
    }

    // ====================================================================
    //  AddCategory
    // ====================================================================

    [Fact]
    public void AddCategory_CreatesAndReturnsId()
    {
        var id = _service.AddCategory("测试分类", "#FF0000");

        Assert.True(id > 0);

        var retrieved = _service.GetCategory(id);
        Assert.NotNull(retrieved);
        Assert.Equal("测试分类", retrieved!.Name);
        Assert.Equal("#FF0000", retrieved.Color);
    }

    [Fact]
    public void AddCategory_IncreasesCountByOne()
    {
        var initialCount = _service.GetAllCategories().Count;

        _service.AddCategory("新分类", "#00FF00");

        var newCount = _service.GetAllCategories().Count;
        Assert.Equal(initialCount + 1, newCount);
    }

    // ====================================================================
    //  UpdateCategory
    // ====================================================================

    [Fact]
    public void UpdateCategory_UpdatesNameAndColor()
    {
        var id = _service.AddCategory("原名称", "#000000");

        _service.UpdateCategory(id, "新名称", "#FFFFFF");

        var updated = _service.GetCategory(id);
        Assert.NotNull(updated);
        Assert.Equal("新名称", updated!.Name);
        Assert.Equal("#FFFFFF", updated.Color);
    }

    // ====================================================================
    //  DeleteCategory — no apps referencing
    // ====================================================================

    [Fact]
    public void DeleteCategory_NoReferencingApps_Succeeds()
    {
        var id = _service.AddCategory("待删除", "#123456");
        Assert.NotNull(_service.GetCategory(id));

        _service.DeleteCategory(id);

        Assert.Null(_service.GetCategory(id));
    }

    [Fact]
    public void DeleteCategory_DefaultCategoryWithNoApps_Succeeds()
    {
        // "学习" category has no apps assigned by default
        var learnCategory = _service.GetAllCategories().First(c => c.Name == "学习");

        _service.DeleteCategory(learnCategory.CategoryId);

        Assert.Null(_service.GetCategory(learnCategory.CategoryId));
    }

    // ====================================================================
    //  DeleteCategory — apps referencing (should throw)
    // ====================================================================

    [Fact]
    public void DeleteCategory_WithReferencingApps_Throws()
    {
        // Arrange: create an app and assign it to a category
        var workCategory = _service.GetAllCategories().First(c => c.Name == "工作");
        var app = _appRepo.GetOrCreateApp(@"C:\test\chrome.exe", "chrome.exe", "Chrome");
        _appRepo.UpdateCategory(app!.AppId, workCategory.CategoryId);

        // Act + Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => _service.DeleteCategory(workCategory.CategoryId));
        Assert.Contains("1", ex.Message);
        Assert.Contains("应用", ex.Message);
    }

    [Fact]
    public void DeleteCategory_WithMultipleReferencingApps_ThrowsWithCount()
    {
        var workCategory = _service.GetAllCategories().First(c => c.Name == "工作");
        var app1 = _appRepo.GetOrCreateApp(@"C:\test\app1.exe", "app1.exe");
        var app2 = _appRepo.GetOrCreateApp(@"C:\test\app2.exe", "app2.exe");
        var app3 = _appRepo.GetOrCreateApp(@"C:\test\app3.exe", "app3.exe");
        _appRepo.UpdateCategory(app1!.AppId, workCategory.CategoryId);
        _appRepo.UpdateCategory(app2!.AppId, workCategory.CategoryId);
        _appRepo.UpdateCategory(app3!.AppId, workCategory.CategoryId);

        var ex = Assert.Throws<InvalidOperationException>(
            () => _service.DeleteCategory(workCategory.CategoryId));
        Assert.Contains("3", ex.Message);
    }

    // ====================================================================
    //  DeleteCategoryAndUnassign
    // ====================================================================

    [Fact]
    public void DeleteCategoryAndUnassign_UnassignsAppsAndDeletes()
    {
        // Arrange
        var workCategory = _service.GetAllCategories().First(c => c.Name == "工作");
        var app = _appRepo.GetOrCreateApp(@"C:\test\chrome.exe", "chrome.exe", "Chrome");
        _appRepo.UpdateCategory(app!.AppId, workCategory.CategoryId);

        // Verify app is assigned and recognized
        var appBefore = _appRepo.GetById(app.AppId);
        Assert.NotNull(appBefore);
        Assert.Equal(workCategory.CategoryId, appBefore!.CategoryId);
        Assert.True(appBefore.IsRecognized);

        // Act
        _service.DeleteCategoryAndUnassign(workCategory.CategoryId);

        // Assert: category is deleted
        Assert.Null(_service.GetCategory(workCategory.CategoryId));

        // Assert: app's category_id is null and is_recognized is false
        var appAfter = _appRepo.GetById(app.AppId);
        Assert.NotNull(appAfter);
        Assert.Null(appAfter!.CategoryId);
        Assert.False(appAfter.IsRecognized);
    }

    [Fact]
    public void DeleteCategoryAndUnassign_NoApps_StillDeletes()
    {
        var id = _service.AddCategory("空分类", "#000000");

        _service.DeleteCategoryAndUnassign(id);

        Assert.Null(_service.GetCategory(id));
    }

    [Fact]
    public void DeleteCategoryAndUnassign_MultipleApps_AllUnassigned()
    {
        var workCategory = _service.GetAllCategories().First(c => c.Name == "工作");
        var app1 = _appRepo.GetOrCreateApp(@"C:\test\app1.exe", "app1.exe");
        var app2 = _appRepo.GetOrCreateApp(@"C:\test\app2.exe", "app2.exe");
        _appRepo.UpdateCategory(app1!.AppId, workCategory.CategoryId);
        _appRepo.UpdateCategory(app2!.AppId, workCategory.CategoryId);

        _service.DeleteCategoryAndUnassign(workCategory.CategoryId);

        var apps = _appRepo.GetAllApps();
        Assert.All(apps, a =>
        {
            Assert.Null(a.CategoryId);
            Assert.False(a.IsRecognized);
        });
    }
}
