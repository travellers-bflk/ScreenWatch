using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Provides aggregation queries over usage session data.
/// </summary>
public interface IUsageQueryService
{
    /// <summary>
    /// Per-application usage statistics (foreground + background separated) within a time range.
    /// Results are sorted by foreground seconds descending.
    /// </summary>
    List<AppUsageStat> GetAppStatsByRange(DateTime start, DateTime end);

    /// <summary>
    /// Per-category aggregated statistics with per-app detail within a time range.
    /// Apps without a category are grouped under "未分类".
    /// Results are sorted by total seconds descending.
    /// </summary>
    List<CategoryStat> GetCategoryStatsByRange(DateTime start, DateTime end);

    /// <summary>
    /// Per-time-period statistics for a single day. Each session is assigned to the
    /// period whose time window contains the session's start time.
    /// Results are sorted by period start time.
    /// </summary>
    List<TimePeriodStat> GetTimePeriodStats(DateTime date);

    /// <summary>
    /// Per-day statistics within a range, used for trend/calendar views.
    /// Days with no sessions are returned with zero values.
    /// Results are sorted by date ascending.
    /// </summary>
    List<DailyStat> GetDailyStats(DateTime start, DateTime end);
}
