namespace ScreenWatch.Core.Models;

/// <summary>
/// Represents a whitelist exclusion rule for filtering tracked applications.
/// </summary>
public class ExclusionRule
{
    public int RuleId { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
