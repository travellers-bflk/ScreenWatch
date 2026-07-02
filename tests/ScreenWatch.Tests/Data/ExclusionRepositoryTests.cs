using ScreenWatch.Core.Data;

namespace ScreenWatch.Tests.Data;

public class ExclusionRepositoryTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly ExclusionRepository _repo;

    public ExclusionRepositoryTests()
    {
        _testDb = new TestDatabase();
        _repo = new ExclusionRepository(_testDb.Db);
    }

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public void IsExcluded_NoRules_ReturnsFalse()
    {
        var result = _repo.IsExcluded(@"C:\Windows\notepad.exe", "notepad.exe");
        Assert.False(result);
    }

    [Fact]
    public void IsExcluded_ExactMatchExeName_ReturnsTrue()
    {
        _repo.Add("exe_name", "notepad.exe");

        var result = _repo.IsExcluded(@"C:\Windows\System32\notepad.exe", "notepad.exe");
        Assert.True(result);
    }

    [Fact]
    public void IsExcluded_NonMatchingExeName_ReturnsFalse()
    {
        _repo.Add("exe_name", "notepad.exe");

        var result = _repo.IsExcluded(@"C:\Program Files\chrome.exe", "chrome.exe");
        Assert.False(result);
    }

    [Fact]
    public void IsExcluded_WildcardExePath_ReturnsTrue()
    {
        // Pattern with % wildcard matches any path under C:\Windows\
        _repo.Add("exe_path", @"C:\Windows\%");

        var result = _repo.IsExcluded(@"C:\Windows\System32\cmd.exe", "cmd.exe");
        Assert.True(result);
    }

    [Fact]
    public void IsExcluded_WildcardExePath_NonMatchingPath_ReturnsFalse()
    {
        _repo.Add("exe_path", @"C:\Windows\%");

        var result = _repo.IsExcluded(@"C:\Program Files\app.exe", "app.exe");
        Assert.False(result);
    }

    [Fact]
    public void IsExcluded_DisabledRule_ReturnsFalse()
    {
        var ruleId = _repo.Add("exe_name", "notepad.exe");
        _repo.Toggle(ruleId, enabled: false);

        var result = _repo.IsExcluded(@"C:\Windows\notepad.exe", "notepad.exe");
        Assert.False(result);
    }

    [Fact]
    public void IsExcluded_MultipleRules_OneMatches_ReturnsTrue()
    {
        _repo.Add("exe_name", "chrome.exe");
        _repo.Add("exe_path", @"C:\Windows\%");

        var result = _repo.IsExcluded(@"C:\Windows\System32\cmd.exe", "cmd.exe");
        Assert.True(result);
    }

    [Fact]
    public void Toggle_ReEnablesRule()
    {
        var ruleId = _repo.Add("exe_name", "notepad.exe");

        _repo.Toggle(ruleId, enabled: false);
        Assert.False(_repo.IsExcluded(@"C:\test\notepad.exe", "notepad.exe"));

        _repo.Toggle(ruleId, enabled: true);
        Assert.True(_repo.IsExcluded(@"C:\test\notepad.exe", "notepad.exe"));
    }

    [Fact]
    public void GetAll_ReturnsAllRules()
    {
        _repo.Add("exe_name", "app1.exe");
        _repo.Add("exe_path", @"C:\Tools\%");

        var rules = _repo.GetAll();
        Assert.Equal(2, rules.Count);
    }

    [Fact]
    public void Remove_DeletesRule()
    {
        var ruleId = _repo.Add("exe_name", "notepad.exe");
        Assert.True(_repo.IsExcluded(@"C:\test\notepad.exe", "notepad.exe"));

        _repo.Remove(ruleId);

        Assert.False(_repo.IsExcluded(@"C:\test\notepad.exe", "notepad.exe"));
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void Add_ReturnsValidRuleId()
    {
        var id1 = _repo.Add("exe_name", "app1.exe");
        var id2 = _repo.Add("exe_name", "app2.exe");

        Assert.True(id1 > 0);
        Assert.True(id2 > id1);
    }
}
