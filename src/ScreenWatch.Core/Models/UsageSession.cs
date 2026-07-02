namespace ScreenWatch.Core.Models;

/// <summary>
/// Represents a single usage session of an application.
/// </summary>
public class UsageSession
{
    public int SessionId { get; set; }
    public int AppId { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationSec { get; set; }
    public bool IsIdle { get; set; }
    public bool IsLocked { get; set; }
}
