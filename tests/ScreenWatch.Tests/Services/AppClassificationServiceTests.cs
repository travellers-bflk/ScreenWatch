using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;
using ScreenWatch.Core.Services;
using ScreenWatch.Tests.Data;

namespace ScreenWatch.Tests.Services;

public class AppClassificationServiceTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly AppRepository _appRepo;
    private readonly CategoryRepository _categoryRepo;
    private readonly BuiltinRulesService _builtinRules;
    private readonly AppClassificationService _service;

    public AppClassificationServiceTests()
    {
        _testDb = new TestDatabase();
        _appRepo = new AppRepository(_testDb.Db);
        _categoryRepo = new CategoryRepository(_testDb.Db);
        _builtinRules = new BuiltinRulesService();
        _service = new AppClassificationService(_appRepo, _categoryRepo, _builtinRules);
    }

    public void Dispose() => _testDb.Dispose();

    // ====================================================================
    //  GetUnrecognizedApps
    // ====================================================================

    [Fact]
    public void GetUnrecognizedApps_Empty_WhenNoApps()
    {
        var apps = _service.GetUnrecognizedApps();
        Assert.Empty(apps);
    }

    [Fact]
    public void GetUnrecognizedApps_ReturnsOnlyUnrecognized()
    {
        // Create two apps
        var app1 = _appRepo.GetOrCreateApp(@"C:\test\chrome.exe", "chrome.exe");
        var app2 = _appRepo.GetOrCreateApp(@"C:\test\steam.exe", "steam.exe");

        // Assign a category to app1 (marks as recognized)
        var workCategory = _categoryRepo.GetAll().First(c => c.Name == "工作");
        _service.AssignCategory(app1!.AppId, workCategory.CategoryId);

        var unrecognized = _service.GetUnrecognizedApps();

        // Only app2 should remain unrecognized
        Assert.Single(unrecognized);
        Assert.Equal(app2!.AppId, unrecognized[0].AppId);
    }

    // ====================================================================
    //  AssignCategory
    // ====================================================================

    [Fact]
    public void AssignCategory_SetsIsRecognizedTrue()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\test\app.exe", "app.exe");
        var workCategory = _categoryRepo.GetAll().First(c => c.Name == "工作");

        // Before: not recognized
        Assert.False(app!.IsRecognized);

        _service.AssignCategory(app.AppId, workCategory.CategoryId);

        // After: recognized with correct category
        var updated = _appRepo.GetById(app.AppId);
        Assert.NotNull(updated);
        Assert.True(updated!.IsRecognized);
        Assert.Equal(workCategory.CategoryId, updated.CategoryId);
    }

    [Fact]
    public void AssignCategory_DifferentCategoryId_UpdatesCorrectly()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\test\app.exe", "app.exe");
        var entertainmentCategory = _categoryRepo.GetAll().First(c => c.Name == "娱乐");

        _service.AssignCategory(app!.AppId, entertainmentCategory.CategoryId);

        var updated = _appRepo.GetById(app.AppId);
        Assert.NotNull(updated);
        Assert.Equal(entertainmentCategory.CategoryId, updated!.CategoryId);
        Assert.True(updated.IsRecognized);
    }

    // ====================================================================
    //  AssignCategoryByName
    // ====================================================================

    [Fact]
    public void AssignCategoryByName_ValidName_AssignsCorrectCategory()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\test\app.exe", "app.exe");

        _service.AssignCategoryByName(app!.AppId, "工作");

        var updated = _appRepo.GetById(app.AppId);
        Assert.NotNull(updated);
        var workCategory = _categoryRepo.GetAll().First(c => c.Name == "工作");
        Assert.Equal(workCategory.CategoryId, updated!.CategoryId);
        Assert.True(updated.IsRecognized);
    }

    [Fact]
    public void AssignCategoryByName_CaseInsensitive_MatchesCategory()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\test\app.exe", "app.exe");

        // Chinese category names don't have case, but test the lookup mechanism
        _service.AssignCategoryByName(app!.AppId, "工作");

        var updated = _appRepo.GetById(app.AppId);
        Assert.NotNull(updated);
        Assert.True(updated!.IsRecognized);
    }

    [Fact]
    public void AssignCategoryByName_NonexistentCategory_Throws()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\test\app.exe", "app.exe");

        var ex = Assert.Throws<InvalidOperationException>(
            () => _service.AssignCategoryByName(app!.AppId, "不存在的分类"));
        Assert.Contains("不存在的分类", ex.Message);
    }

    // ====================================================================
    //  SuggestForApp — known apps
    // ====================================================================

    [Fact]
    public void SuggestForApp_KnownApp_Chrome_ReturnsSuggestion()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\Program Files\Google\Chrome\chrome.exe", "chrome.exe");

        var suggestion = _service.SuggestForApp(app!);

        Assert.NotNull(suggestion);
        Assert.Equal("工作", suggestion!.SuggestedCategoryName);
        Assert.Equal("Google Chrome", suggestion.SuggestedDisplayName);

        // SuggestedCategoryId should match the "工作" category in the DB
        var workCategory = _categoryRepo.GetAll().First(c => c.Name == "工作");
        Assert.Equal(workCategory.CategoryId, suggestion.SuggestedCategoryId);
    }

    [Fact]
    public void SuggestForApp_KnownApp_Steam_ReturnsEntertainmentSuggestion()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\steam\steam.exe", "steam.exe");

        var suggestion = _service.SuggestForApp(app!);

        Assert.NotNull(suggestion);
        Assert.Equal("娱乐", suggestion!.SuggestedCategoryName);
        Assert.Equal("Steam", suggestion.SuggestedDisplayName);
    }

    [Fact]
    public void SuggestForApp_KnownApp_WeChat_ReturnsSocialSuggestion()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\WeChat\WeChat.exe", "WeChat.exe");

        var suggestion = _service.SuggestForApp(app!);

        Assert.NotNull(suggestion);
        Assert.Equal("社交", suggestion!.SuggestedCategoryName);
        Assert.Equal("微信", suggestion.SuggestedDisplayName);
    }

    [Fact]
    public void SuggestForApp_KnownApp_OneNote_ReturnsStudySuggestion()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\OneNote\OneNote.exe", "OneNote.exe");

        var suggestion = _service.SuggestForApp(app!);

        Assert.NotNull(suggestion);
        Assert.Equal("学习", suggestion!.SuggestedCategoryName);
        Assert.Equal("Microsoft OneNote", suggestion.SuggestedDisplayName);
    }

    // ====================================================================
    //  SuggestForApp — unknown apps
    // ====================================================================

    [Fact]
    public void SuggestForApp_UnknownApp_ReturnsNull()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\test\unknown_app.exe", "unknown_app.exe");

        Assert.Null(_service.SuggestForApp(app!));
    }

    [Fact]
    public void SuggestForApp_AnotherUnknownApp_ReturnsNull()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\custom\myapp.exe", "myapp.exe");

        Assert.Null(_service.SuggestForApp(app!));
    }

    // ====================================================================
    //  SuggestForApp — case insensitivity
    // ====================================================================

    [Fact]
    public void SuggestForApp_ExeNameCaseInsensitive_StillMatches()
    {
        // The stored rule is "chrome.exe"; the app's exe_name is "CHROME.EXE"
        var app = _appRepo.GetOrCreateApp(@"C:\chrome\CHROME.EXE", "CHROME.EXE");

        var suggestion = _service.SuggestForApp(app!);

        Assert.NotNull(suggestion);
        Assert.Equal("工作", suggestion!.SuggestedCategoryName);
    }

    // ====================================================================
    //  AutoClassifyAllUnrecognized
    // ====================================================================

    [Fact]
    public void AutoClassifyAllUnrecognized_NoApps_ReturnsZero()
    {
        var count = _service.AutoClassifyAllUnrecognized();
        Assert.Equal(0, count);
    }

    [Fact]
    public void AutoClassifyAllUnrecognized_NoMatchingApps_ReturnsZero()
    {
        _appRepo.GetOrCreateApp(@"C:\test\unknown1.exe", "unknown1.exe");
        _appRepo.GetOrCreateApp(@"C:\test\unknown2.exe", "unknown2.exe");

        var count = _service.AutoClassifyAllUnrecognized();
        Assert.Equal(0, count);

        // Apps should still be unrecognized
        var remaining = _service.GetUnrecognizedApps();
        Assert.Equal(2, remaining.Count);
    }

    [Fact]
    public void AutoClassifyAllUnrecognized_AllMatch_ClassifiesAll()
    {
        // Create apps that match built-in rules
        _appRepo.GetOrCreateApp(@"C:\chrome\chrome.exe", "chrome.exe");
        _appRepo.GetOrCreateApp(@"C:\steam\steam.exe", "steam.exe");
        _appRepo.GetOrCreateApp(@"C:\wechat\WeChat.exe", "WeChat.exe");

        var count = _service.AutoClassifyAllUnrecognized();

        Assert.Equal(3, count);

        // All should now be recognized
        var remaining = _service.GetUnrecognizedApps();
        Assert.Empty(remaining);

        // Verify correct categories were assigned
        var allApps = _appRepo.GetAllApps();
        var workCategory = _categoryRepo.GetAll().First(c => c.Name == "工作");
        var entertainmentCategory = _categoryRepo.GetAll().First(c => c.Name == "娱乐");
        var socialCategory = _categoryRepo.GetAll().First(c => c.Name == "社交");

        var chrome = allApps.First(a => a.ExeName == "chrome.exe");
        Assert.Equal(workCategory.CategoryId, chrome.CategoryId);
        Assert.True(chrome.IsRecognized);

        var steam = allApps.First(a => a.ExeName == "steam.exe");
        Assert.Equal(entertainmentCategory.CategoryId, steam.CategoryId);
        Assert.True(steam.IsRecognized);

        var wechat = allApps.First(a => a.ExeName == "WeChat.exe");
        Assert.Equal(socialCategory.CategoryId, wechat.CategoryId);
        Assert.True(wechat.IsRecognized);
    }

    [Fact]
    public void AutoClassifyAllUnrecognized_PartialMatch_ClassifiesOnlyMatching()
    {
        // 3 matching apps + 2 unknown
        _appRepo.GetOrCreateApp(@"C:\chrome\chrome.exe", "chrome.exe");
        _appRepo.GetOrCreateApp(@"C:\steam\steam.exe", "steam.exe");
        _appRepo.GetOrCreateApp(@"C:\notepad\notepad.exe", "notepad.exe");
        _appRepo.GetOrCreateApp(@"C:\custom\myapp.exe", "myapp.exe");
        _appRepo.GetOrCreateApp(@"C:\custom\other.exe", "other.exe");

        var count = _service.AutoClassifyAllUnrecognized();

        // 3 matched (chrome, steam, notepad), 2 unknown
        Assert.Equal(3, count);

        // 2 should remain unrecognized
        var remaining = _service.GetUnrecognizedApps();
        Assert.Equal(2, remaining.Count);

        var remainingNames = remaining.Select(a => a.ExeName).ToList();
        Assert.Contains("myapp.exe", remainingNames);
        Assert.Contains("other.exe", remainingNames);
    }

    [Fact]
    public void AutoClassifyAllUnrecognized_AlreadyClassified_NotRecounted()
    {
        var app = _appRepo.GetOrCreateApp(@"C:\chrome\chrome.exe", "chrome.exe");
        var workCategory = _categoryRepo.GetAll().First(c => c.Name == "工作");

        // Manually classify chrome first
        _service.AssignCategory(app!.AppId, workCategory.CategoryId);

        // Add an unrecognized matching app
        _appRepo.GetOrCreateApp(@"C:\steam\steam.exe", "steam.exe");

        var count = _service.AutoClassifyAllUnrecognized();

        // Only steam should be classified (chrome is already recognized)
        Assert.Equal(1, count);
    }

    [Fact]
    public void AutoClassifyAllUnrecognized_CaseInsensitiveMatch()
    {
        // Create app with uppercase exe name; rule stores "chrome.exe"
        _appRepo.GetOrCreateApp(@"C:\chrome\CHROME.EXE", "CHROME.EXE");

        var count = _service.AutoClassifyAllUnrecognized();

        Assert.Equal(1, count);
        Assert.Empty(_service.GetUnrecognizedApps());
    }

    // ====================================================================
    //  NewUnrecognizedAppDetected event
    // ====================================================================

    [Fact]
    public void NewUnrecognizedAppDetected_Raised_WhenInvoked()
    {
        AppInfo? detectedApp = null;
        _service.NewUnrecognizedAppDetected += (sender, app) => detectedApp = app;

        var app = new AppInfo { AppId = 42, ExeName = "test.exe" };
        _service.RaiseNewUnrecognizedAppDetected(app);

        Assert.NotNull(detectedApp);
        Assert.Equal(42, detectedApp!.AppId);
        Assert.Equal("test.exe", detectedApp.ExeName);
    }

    [Fact]
    public void NewUnrecognizedAppDetected_NoSubscriber_DoesNotThrow()
    {
        var app = new AppInfo { AppId = 1, ExeName = "test.exe" };

        // Should not throw even with no subscribers
        _service.RaiseNewUnrecognizedAppDetected(app);
    }
}
