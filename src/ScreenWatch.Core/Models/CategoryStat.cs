namespace ScreenWatch.Core.Models;

/// <summary>
/// Aggregated statistics for a category, including the apps within it.
/// </summary>
public class CategoryStat
{
    public int? CategoryId { get; set; }

    /// <summary>
    /// Category name. "未分类" when the app has no category assigned.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    public string? Color { get; set; }

    public int ForegroundSeconds { get; set; }
    public int BackgroundSeconds { get; set; }
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;

    /// <summary>
    /// Per-app detail list belonging to this category.
    /// </summary>
    public List<AppUsageStat> Apps { get; set; } = new();
}
