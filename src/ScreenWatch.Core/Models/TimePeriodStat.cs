namespace ScreenWatch.Core.Models;

/// <summary>
/// Usage statistics for a configured time period within a single day.
/// </summary>
public class TimePeriodStat
{
    public int PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;

    public int ForegroundSeconds { get; set; }
    public int BackgroundSeconds { get; set; }
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;
}
