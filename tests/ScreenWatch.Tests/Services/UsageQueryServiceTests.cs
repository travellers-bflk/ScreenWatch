using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;
using ScreenWatch.Core.Services;
using ScreenWatch.Tests.Data;

namespace ScreenWatch.Tests.Services;

public class UsageQueryServiceTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly SessionRepository _sessionRepo;
    private readonly AppRepository _appRepo;
    private readonly CategoryRepository _categoryRepo;
    private readonly TimePeriodRepository _timePeriodRepo;
    private readonly UsageQueryService _queryService;

    public UsageQueryServiceTests()
    {
        _testDb = new TestDatabase();
        _sessionRepo = new SessionRepository(_testDb.Db);
        _appRepo = new AppRepository(_testDb.Db);
        _categoryRepo = new CategoryRepository(_testDb.Db);
        _timePeriodRepo = new TimePeriodRepository(_testDb.Db);
        _queryService = new UsageQueryService(_sessionRepo, _appRepo, _categoryRepo, _timePeriodRepo);
    }

    public void Dispose() => _testDb.Dispose();

    /// <summary>
    /// Creates an app via GetOrCreateApp, optionally assigning it to a category.
    /// </summary>
    private AppInfo CreateApp(string exeName, string displayName, int? categoryId = null)
    {
        var app = _appRepo.GetOrCreateApp($@"C:\test\{exeName}", exeName, displayName)!;
        if (categoryId.HasValue)
            _appRepo.UpdateCategory(app.AppId, categoryId.Value);
        return app;
    }

    private void InsertSession(int appId, string sessionType, DateTime start, int durationSec)
    {
        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = appId,
            SessionType = sessionType,
            StartTime = start,
            EndTime = start.AddSeconds(durationSec),
            DurationSec = durationSec
        });
    }

    // 2026-01-15 is a Thursday (DayOfWeek = 4)
    private static readonly DateTime TestDate = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    #region GetAppStatsByRange

    [Fact]
    public void GetAppStatsByRange_AccumulatesForegroundAndBackgroundSeparately()
    {
        var app1 = CreateApp("app1.exe", "App One");
        var app2 = CreateApp("app2.exe", "App Two");
        var baseTime = TestDate.AddHours(10);

        // App1: 300 fg + 100 fg = 400 fg, 200 bg
        InsertSession(app1.AppId, "foreground", baseTime, 300);
        InsertSession(app1.AppId, "background", baseTime.AddMinutes(10), 200);
        InsertSession(app1.AppId, "foreground", baseTime.AddMinutes(20), 100);

        // App2: 500 fg only
        InsertSession(app2.AppId, "foreground", baseTime.AddMinutes(5), 500);

        var stats = _queryService.GetAppStatsByRange(baseTime.AddMinutes(-5), baseTime.AddMinutes(60));

        Assert.Equal(2, stats.Count);
        // Sorted by foreground descending: App2 (500) first, App1 (400) second
        Assert.Equal(app2.AppId, stats[0].AppId);
        Assert.Equal(500, stats[0].ForegroundSeconds);
        Assert.Equal(0, stats[0].BackgroundSeconds);
        Assert.Equal(500, stats[0].TotalSeconds);
        Assert.Equal("App Two", stats[0].DisplayName);
        Assert.Equal("app2.exe", stats[0].ExeName);

        Assert.Equal(app1.AppId, stats[1].AppId);
        Assert.Equal(400, stats[1].ForegroundSeconds);
        Assert.Equal(200, stats[1].BackgroundSeconds);
        Assert.Equal(600, stats[1].TotalSeconds);
    }

    [Fact]
    public void GetAppStatsByRange_FillsCategoryName()
    {
        // "开发" is seeded by V2 migration — look it up instead of adding
        var catId = _categoryRepo.GetAll().First(c => c.Name == "开发").CategoryId;
        var app = CreateApp("word.exe", "Word", catId);
        InsertSession(app.AppId, "foreground", TestDate.AddHours(10), 300);

        var stats = _queryService.GetAppStatsByRange(TestDate, TestDate.AddDays(1));

        Assert.Single(stats);
        Assert.Equal(catId, stats[0].CategoryId);
        Assert.Equal("开发", stats[0].CategoryName);
    }

    [Fact]
    public void GetAppStatsByRange_NoSessions_ReturnsEmptyList()
    {
        var stats = _queryService.GetAppStatsByRange(TestDate, TestDate.AddDays(1));
        Assert.Empty(stats);
    }

    #endregion

    #region GetCategoryStatsByRange

    [Fact]
    public void GetCategoryStatsByRange_AggregatesByCategoryWithUncategorized()
    {
        // "开发" and "游戏" are seeded by V2 migration — look them up
        var workCatId = _categoryRepo.GetAll().First(c => c.Name == "开发").CategoryId;
        var gameCatId = _categoryRepo.GetAll().First(c => c.Name == "游戏").CategoryId;

        var workApp = CreateApp("word.exe", "Word", workCatId);
        var gameApp = CreateApp("game.exe", "Game", gameCatId);
        var miscApp = CreateApp("misc.exe", "Misc");

        var baseTime = TestDate.AddHours(10);
        InsertSession(workApp.AppId, "foreground", baseTime, 400);
        InsertSession(gameApp.AppId, "foreground", baseTime.AddMinutes(10), 200);
        InsertSession(gameApp.AppId, "background", baseTime.AddMinutes(20), 100);
        InsertSession(miscApp.AppId, "foreground", baseTime.AddMinutes(30), 150);

        var stats = _queryService.GetCategoryStatsByRange(baseTime.AddMinutes(-5), baseTime.AddMinutes(60));

        // 3 buckets: 开发(400), 游戏(300), 未分类(150)
        Assert.Equal(3, stats.Count);
        // Sorted by total descending
        Assert.Equal("开发", stats[0].CategoryName);
        Assert.Equal("#9B59B6", stats[0].Color); // seeded "开发" color
        Assert.Equal(400, stats[0].ForegroundSeconds);
        Assert.Equal(0, stats[0].BackgroundSeconds);
        Assert.Single(stats[0].Apps);

        Assert.Equal("游戏", stats[1].CategoryName);
        Assert.Equal(200, stats[1].ForegroundSeconds);
        Assert.Equal(100, stats[1].BackgroundSeconds);
        Assert.Equal(300, stats[1].TotalSeconds);
        Assert.Single(stats[1].Apps);

        Assert.Equal("未分类", stats[2].CategoryName);
        Assert.Null(stats[2].CategoryId);
        Assert.Equal(150, stats[2].ForegroundSeconds);
        Assert.Equal(0, stats[2].BackgroundSeconds);
        Assert.Single(stats[2].Apps);
    }

    [Fact]
    public void GetCategoryStatsByRange_NoSessions_ReturnsEmptyList()
    {
        var stats = _queryService.GetCategoryStatsByRange(TestDate, TestDate.AddDays(1));
        Assert.Empty(stats);
    }

    #endregion

    #region GetTimePeriodStats

    [Fact]
    public void GetTimePeriodStats_AssignsSessionsToCorrectPeriods()
    {
        // Four periods including a cross-midnight night period.
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "上午", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            Weekdays = "1,2,3,4,5", Enabled = true
        });
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "下午", StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0),
            Weekdays = "1,2,3,4,5", Enabled = true
        });
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "晚间", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(22, 0),
            Weekdays = "1,2,3,4,5", Enabled = true
        });
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "夜间", StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(6, 0),
            Weekdays = "1,2,3,4,5", Enabled = true
        });

        var app = CreateApp("app.exe", "App");
        // Thursday = dayOfWeek 4, included in "1,2,3,4,5"
        InsertSession(app.AppId, "foreground", TestDate.AddHours(10), 300);   // 10:00 → 上午
        InsertSession(app.AppId, "background", TestDate.AddHours(14), 200);   // 14:00 → 下午
        InsertSession(app.AppId, "foreground", TestDate.AddHours(19), 100);   // 19:00 → 晚间
        InsertSession(app.AppId, "foreground", TestDate.AddHours(23), 150);   // 23:00 → 夜间
        InsertSession(app.AppId, "foreground", TestDate.AddHours(2), 50);     // 02:00 → 夜间 (cross-midnight)

        var stats = _queryService.GetTimePeriodStats(TestDate);

        Assert.Equal(4, stats.Count);
        // Ordered by start time: 上午(09:00), 下午(13:00), 晚间(18:00), 夜间(22:00)
        Assert.Equal("上午", stats[0].PeriodName);
        Assert.Equal(300, stats[0].ForegroundSeconds);
        Assert.Equal(0, stats[0].BackgroundSeconds);

        Assert.Equal("下午", stats[1].PeriodName);
        Assert.Equal(0, stats[1].ForegroundSeconds);
        Assert.Equal(200, stats[1].BackgroundSeconds);

        Assert.Equal("晚间", stats[2].PeriodName);
        Assert.Equal(100, stats[2].ForegroundSeconds);

        Assert.Equal("夜间", stats[3].PeriodName);
        Assert.Equal(200, stats[3].ForegroundSeconds); // 150 + 50
        Assert.Equal(0, stats[3].BackgroundSeconds);
    }

    [Fact]
    public void GetTimePeriodStats_CrossMidnightPeriod_CatchesEarlyMorningAndLateNight()
    {
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "夜间", StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(6, 0),
            Weekdays = "", Enabled = true // empty = all days
        });

        var app = CreateApp("app.exe", "App");
        InsertSession(app.AppId, "foreground", TestDate.AddHours(23), 100);  // 23:00 → in
        InsertSession(app.AppId, "foreground", TestDate.AddHours(2), 200);   // 02:00 → in
        InsertSession(app.AppId, "foreground", TestDate.AddHours(10), 300);  // 10:00 → NOT in

        var stats = _queryService.GetTimePeriodStats(TestDate);

        Assert.Single(stats);
        Assert.Equal("夜间", stats[0].PeriodName);
        Assert.Equal(300, stats[0].ForegroundSeconds); // 100 + 200, the 10:00 session excluded
    }

    [Fact]
    public void GetTimePeriodStats_WeekdaysFilter_ExcludesNonMatchingDay()
    {
        // Period with weekdays "1,2,3" (Mon-Wed). Thursday (4) should be excluded.
        var periodAId = _timePeriodRepo.Add(new TimePeriod
        {
            Name = "工作日早间", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            Weekdays = "1,2,3", Enabled = true
        });
        // Period with weekdays "4,5" (Thu-Fri). Thursday (4) should be included.
        var periodBId = _timePeriodRepo.Add(new TimePeriod
        {
            Name = "周四五早间", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            Weekdays = "4,5", Enabled = true
        });

        var app = CreateApp("app.exe", "App");
        // Thursday 10:00 → matches periodB only
        InsertSession(app.AppId, "foreground", TestDate.AddHours(10), 300);

        var stats = _queryService.GetTimePeriodStats(TestDate);

        Assert.Equal(2, stats.Count);
        var statA = stats.First(s => s.PeriodId == periodAId);
        var statB = stats.First(s => s.PeriodId == periodBId);
        Assert.Equal(0, statA.ForegroundSeconds); // excluded by weekdays filter
        Assert.Equal(300, statB.ForegroundSeconds); // included
    }

    [Fact]
    public void GetTimePeriodStats_NoPeriods_ReturnsEmptyList()
    {
        var app = CreateApp("app.exe", "App");
        InsertSession(app.AppId, "foreground", TestDate.AddHours(10), 300);

        var stats = _queryService.GetTimePeriodStats(TestDate);
        Assert.Empty(stats);
    }

    [Fact]
    public void GetTimePeriodStats_NoSessions_ReturnsAllPeriodsWithZeros()
    {
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "上午", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            Weekdays = "1,2,3,4,5", Enabled = true
        });
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "下午", StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0),
            Weekdays = "1,2,3,4,5", Enabled = true
        });

        var stats = _queryService.GetTimePeriodStats(TestDate);

        Assert.Equal(2, stats.Count);
        Assert.All(stats, s =>
        {
            Assert.Equal(0, s.ForegroundSeconds);
            Assert.Equal(0, s.BackgroundSeconds);
        });
    }

    [Fact]
    public void GetTimePeriodStats_DisabledPeriodsAreExcluded()
    {
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "启用时段", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            Weekdays = "1,2,3,4,5", Enabled = true
        });
        _timePeriodRepo.Add(new TimePeriod
        {
            Name = "禁用时段", StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0),
            Weekdays = "1,2,3,4,5", Enabled = false
        });

        var app = CreateApp("app.exe", "App");
        InsertSession(app.AppId, "foreground", TestDate.AddHours(14), 300); // 14:00 would match disabled period

        var stats = _queryService.GetTimePeriodStats(TestDate);

        Assert.Single(stats);
        Assert.Equal("启用时段", stats[0].PeriodName);
        Assert.Equal(0, stats[0].ForegroundSeconds); // session fell in disabled period, not counted
    }

    #endregion

    #region GetDailyStats

    [Fact]
    public void GetDailyStats_FillsEmptyDaysWithZero()
    {
        var app = CreateApp("app.exe", "App");
        // Jan 15: 300 fg + 100 bg
        InsertSession(app.AppId, "foreground", TestDate.AddHours(10), 300);
        InsertSession(app.AppId, "background", TestDate.AddHours(14), 100);
        // Jan 17: 200 fg
        InsertSession(app.AppId, "foreground", TestDate.AddDays(2).AddHours(9), 200);

        var stats = _queryService.GetDailyStats(TestDate, TestDate.AddDays(5).AddSeconds(-1));

        // 5 days: Jan 15-19
        Assert.Equal(5, stats.Count);
        Assert.Equal(TestDate.Date, stats[0].Date);
        Assert.Equal(300, stats[0].ForegroundSeconds);
        Assert.Equal(100, stats[0].BackgroundSeconds);
        Assert.Equal(400, stats[0].TotalSeconds);

        // Jan 16: empty
        Assert.Equal(TestDate.AddDays(1).Date, stats[1].Date);
        Assert.Equal(0, stats[1].TotalSeconds);

        // Jan 17: 200 fg
        Assert.Equal(TestDate.AddDays(2).Date, stats[2].Date);
        Assert.Equal(200, stats[2].ForegroundSeconds);
        Assert.Equal(0, stats[2].BackgroundSeconds);

        // Jan 18 and 19: empty
        Assert.Equal(0, stats[3].TotalSeconds);
        Assert.Equal(0, stats[4].TotalSeconds);
    }

    [Fact]
    public void GetDailyStats_NoSessions_ReturnsAllDaysInRangeWithZero()
    {
        var stats = _queryService.GetDailyStats(TestDate, TestDate.AddDays(3).AddSeconds(-1));

        Assert.Equal(3, stats.Count);
        Assert.All(stats, s => Assert.Equal(0, s.TotalSeconds));
        Assert.Equal(TestDate.Date, stats[0].Date);
        Assert.Equal(TestDate.AddDays(1).Date, stats[1].Date);
        Assert.Equal(TestDate.AddDays(2).Date, stats[2].Date);
    }

    [Fact]
    public void GetDailyStats_SortedByDateAscending()
    {
        var app = CreateApp("app.exe", "App");
        // Insert out of order — Jan 17 first, then Jan 15
        InsertSession(app.AppId, "foreground", TestDate.AddDays(2).AddHours(10), 200);
        InsertSession(app.AppId, "foreground", TestDate.AddHours(10), 300);

        var stats = _queryService.GetDailyStats(TestDate, TestDate.AddDays(3).AddSeconds(-1));

        Assert.Equal(3, stats.Count);
        Assert.True(stats[0].Date < stats[1].Date);
        Assert.True(stats[1].Date < stats[2].Date);
        Assert.Equal(300, stats[0].ForegroundSeconds);
        Assert.Equal(0, stats[1].ForegroundSeconds);
        Assert.Equal(200, stats[2].ForegroundSeconds);
    }

    #endregion
}
