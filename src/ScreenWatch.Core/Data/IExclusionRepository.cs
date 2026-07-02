using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public interface IExclusionRepository
{
    List<ExclusionRule> GetAll();
    int Add(string matchType, string pattern);
    void Remove(int ruleId);
    void Toggle(int ruleId, bool enabled);

    /// <summary>
    /// Checks whether the given application is excluded by any enabled whitelist rule.
    /// </summary>
    bool IsExcluded(string exePath, string exeName);
}
