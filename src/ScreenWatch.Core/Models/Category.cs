namespace ScreenWatch.Core.Models;

/// <summary>
/// Represents an application category for grouping and reporting.
/// </summary>
public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Emoji icon for visual identification (e.g. "💼"). May be empty for user-created categories.
    /// </summary>
    public string? Icon { get; set; }
}
