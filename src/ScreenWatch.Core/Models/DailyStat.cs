namespace ScreenWatch.Core.Models;

/// <summary>
/// Per-day usage statistics used for calendar/trend views.
/// </summary>
public class DailyStat
{
    public DateTime Date { get; set; }

    public int ForegroundSeconds { get; set; }
    public int BackgroundSeconds { get; set; }
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;
}
