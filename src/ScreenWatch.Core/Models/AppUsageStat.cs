namespace ScreenWatch.Core.Models;

/// <summary>
/// Single application usage statistics with foreground and background time separated.
/// </summary>
public class AppUsageStat
{
    public int AppId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ExeName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    /// <summary>
    /// Foreground (active) usage duration in seconds.
    /// </summary>
    public int ForegroundSeconds { get; set; }

    /// <summary>
    /// Background (idle/running-but-not-focused) duration in seconds.
    /// </summary>
    public int BackgroundSeconds { get; set; }

    /// <summary>
    /// Total usage duration (foreground + background) in seconds.
    /// </summary>
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;
}
