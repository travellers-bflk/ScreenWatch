namespace ScreenWatch.Core.Models;

/// <summary>
/// Represents a named time period used for scheduled tracking.
/// </summary>
public class TimePeriod
{
    public int PeriodId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Weekdays { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
