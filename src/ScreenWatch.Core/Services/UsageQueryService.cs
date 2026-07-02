using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Aggregates usage session data into statistics by app, category, time period, and day.
/// All aggregation is performed in memory after a single range query, since the yearly
/// data volume is small. Apps and categories are preloaded into dictionaries to avoid
/// per-session lookups.
/// </summary>
public class UsageQueryService : IUsageQueryService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IAppRepository _appRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITimePeriodRepository _timePeriodRepository;

    private const string ForegroundType = "foreground";

    public UsageQueryService(
        ISessionRepository sessionRepository,
        IAppRepository appRepository,
        ICategoryRepository categoryRepository,
        ITimePeriodRepository timePeriodRepository)
    {
        _sessionRepository = sessionRepository;
        _appRepository = appRepository;
        _categoryRepository = categoryRepository;
        _timePeriodRepository = timePeriodRepository;
    }

    /// <inheritdoc />
    public List<AppUsageStat> GetAppStatsByRange(DateTime start, DateTime end)
    {
        var sessions = _sessionRepository.GetSessionsByRange(start, end);
        if (sessions.Count == 0)
            return new List<AppUsageStat>();

        // Preload apps and categories into dictionaries for O(1) lookup.
        var appsById = _appRepository.GetAllApps().ToDictionary(a => a.AppId);
        var categoriesById = _categoryRepository.GetAll().ToDictionary(c => c.CategoryId);

        // Group sessions by AppId and accumulate foreground/background seconds.
        var statsByApp = new Dictionary<int, AppUsageStat>();
        foreach (var session in sessions)
        {
            if (!statsByApp.TryGetValue(session.AppId, out var stat))
            {
                stat = new AppUsageStat { AppId = session.AppId };
                statsByApp[session.AppId] = stat;
            }

            if (session.SessionType == ForegroundType)
                stat.ForegroundSeconds += session.DurationSec;
            else
                stat.BackgroundSeconds += session.DurationSec;
        }

        // Enrich each stat with app/category metadata.
        var result = new List<AppUsageStat>(statsByApp.Count);
        foreach (var stat in statsByApp.Values)
        {
            if (appsById.TryGetValue(stat.AppId, out var app))
            {
                stat.DisplayName = app.DisplayName;
                stat.ExeName = app.ExeName;
                stat.CategoryId = app.CategoryId;
                if (app.CategoryId.HasValue && categoriesById.TryGetValue(app.CategoryId.Value, out var cat))
                    stat.CategoryName = cat.Name;
            }

            result.Add(stat);
        }

        // Sort by foreground seconds descending.
        result.Sort((a, b) => b.ForegroundSeconds.CompareTo(a.ForegroundSeconds));
        return result;
    }

    /// <inheritdoc />
    public List<CategoryStat> GetCategoryStatsByRange(DateTime start, DateTime end)
    {
        var appStats = GetAppStatsByRange(start, end);
        if (appStats.Count == 0)
            return new List<CategoryStat>();

        var categoriesById = _categoryRepository.GetAll().ToDictionary(c => c.CategoryId);

        var result = new List<CategoryStat>();

        // Group app stats by CategoryId (null => "未分类"). Categorized apps first.
        var categorizedGroups = appStats
            .Where(a => a.CategoryId.HasValue)
            .GroupBy(a => a.CategoryId!.Value);

        foreach (var g in categorizedGroups)
        {
            var catStat = new CategoryStat { CategoryId = g.Key };
            foreach (var appStat in g)
            {
                catStat.ForegroundSeconds += appStat.ForegroundSeconds;
                catStat.BackgroundSeconds += appStat.BackgroundSeconds;
                catStat.Apps.Add(appStat);
            }

            if (categoriesById.TryGetValue(g.Key, out var cat))
            {
                catStat.CategoryName = cat.Name;
                catStat.Color = cat.Color;
            }

            result.Add(catStat);
        }

        // Uncategorized bucket.
        var uncategorized = appStats.Where(a => !a.CategoryId.HasValue).ToList();
        if (uncategorized.Count > 0)
        {
            var catStat = new CategoryStat { CategoryId = null, CategoryName = "未分类" };
            foreach (var appStat in uncategorized)
            {
                catStat.ForegroundSeconds += appStat.ForegroundSeconds;
                catStat.BackgroundSeconds += appStat.BackgroundSeconds;
                catStat.Apps.Add(appStat);
            }

            result.Add(catStat);
        }

        // Sort by total seconds descending.
        result.Sort((a, b) => b.TotalSeconds.CompareTo(a.TotalSeconds));
        return result;
    }

    /// <inheritdoc />
    public List<TimePeriodStat> GetTimePeriodStats(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        var sessions = _sessionRepository.GetSessionsByRange(dayStart, dayEnd);

        // Only consider enabled periods, ordered by start time for stable output.
        var periods = _timePeriodRepository.GetAll()
            .Where(p => p.Enabled)
            .OrderBy(p => p.StartTime)
            .ToList();

        if (periods.Count == 0)
            return new List<TimePeriodStat>();

        // Initialize a stat entry for every enabled period so empty periods appear.
        var statByPeriodId = periods.ToDictionary(
            p => p.PeriodId,
            p => new TimePeriodStat { PeriodId = p.PeriodId, PeriodName = p.Name });

        foreach (var session in sessions)
        {
            var sessionTime = TimeOnly.FromDateTime(session.StartTime);
            var dayOfWeek = (int)session.StartTime.DayOfWeek;

            foreach (var period in periods)
            {
                if (!MatchesWeekdays(period, dayOfWeek))
                    continue;

                if (IsInTimeWindow(period, sessionTime))
                {
                    var stat = statByPeriodId[period.PeriodId];
                    if (session.SessionType == ForegroundType)
                        stat.ForegroundSeconds += session.DurationSec;
                    else
                        stat.BackgroundSeconds += session.DurationSec;

                    break; // Each session is assigned to at most one period.
                }
            }
        }

        // Return in period start-time order (already sorted).
        return periods.Select(p => statByPeriodId[p.PeriodId]).ToList();
    }

    /// <inheritdoc />
    public List<DailyStat> GetDailyStats(DateTime start, DateTime end)
    {
        var sessions = _sessionRepository.GetSessionsByRange(start, end);

        var dailyMap = new Dictionary<DateTime, DailyStat>();

        // Pre-populate every day in the range with zero values so empty days are included.
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            dailyMap[day] = new DailyStat { Date = day };
        }

        // Aggregate sessions by their start-time date.
        foreach (var session in sessions)
        {
            var day = session.StartTime.Date;
            if (!dailyMap.TryGetValue(day, out var stat))
            {
                stat = new DailyStat { Date = day };
                dailyMap[day] = stat;
            }

            if (session.SessionType == ForegroundType)
                stat.ForegroundSeconds += session.DurationSec;
            else
                stat.BackgroundSeconds += session.DurationSec;
        }

        return dailyMap.Values.OrderBy(d => d.Date).ToList();
    }

    /// <summary>
    /// Checks whether the given day-of-week integer is included in the period's Weekdays string.
    /// An empty or whitespace Weekdays means all days match.
    /// </summary>
    private static bool MatchesWeekdays(TimePeriod period, int dayOfWeek)
    {
        if (string.IsNullOrWhiteSpace(period.Weekdays))
            return true;

        var days = period.Weekdays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var d in days)
        {
            if (int.TryParse(d, out var value) && value == dayOfWeek)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a time-of-day falls within the period's time window.
    /// When EndTime &gt; StartTime the window is a normal range (e.g. 09:00-12:00).
    /// When EndTime &lt;= StartTime the window crosses midnight (e.g. 22:00-06:00)
    /// or represents a full day when both are equal.
    /// </summary>
    private static bool IsInTimeWindow(TimePeriod period, TimeOnly time)
    {
        if (period.EndTime > period.StartTime)
        {
            return time >= period.StartTime && time < period.EndTime;
        }

        // Crosses midnight or full day.
        return time >= period.StartTime || time < period.EndTime;
    }
}
