namespace ScreenWatch.Core.Models;

/// <summary>
/// Represents a tracked application.
/// </summary>
public class AppInfo
{
    public int AppId { get; set; }
    public string ExePath { get; set; } = string.Empty;
    public string ExeName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? IconCacheKey { get; set; }
    public DateTime FirstSeen { get; set; }
    public bool IsRecognized { get; set; }
}
