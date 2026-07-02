using ScreenWatch.Core.Services;

namespace ScreenWatch.Tests.Services;

public class BuiltinRulesServiceTests
{
    private readonly BuiltinRulesService _service = new();

    // ====================================================================
    //  FindRule — case-insensitive matching
    // ====================================================================

    [Theory]
    [InlineData("chrome.exe")]
    [InlineData("CHROME.EXE")]
    [InlineData("Chrome.exe")]
    [InlineData("ChRoMe.ExE")]
    public void FindRule_MatchesCaseInsensitively(string exeName)
    {
        var rule = _service.FindRule(exeName);

        Assert.NotNull(rule);
        Assert.Equal("chrome.exe", rule!.ExeName);
        Assert.Equal("工作", rule.Category);
        Assert.Equal("Google Chrome", rule.DisplayName);
    }

    [Theory]
    [InlineData("STEAM.EXE")]
    [InlineData("Steam.exe")]
    [InlineData("steam.exe")]
    public void FindRule_MatchesSteamCaseInsensitively(string exeName)
    {
        var rule = _service.FindRule(exeName);

        Assert.NotNull(rule);
        Assert.Equal("娱乐", rule!.Category);
        Assert.Equal("Steam", rule.DisplayName);
    }

    // WINWORD.EXE is stored in uppercase in the JSON; lowercase should still match
    [Theory]
    [InlineData("WINWORD.EXE")]
    [InlineData("winword.exe")]
    [InlineData("WinWord.exe")]
    public void FindRule_MatchesUpperCaseStoredRuleCaseInsensitively(string exeName)
    {
        var rule = _service.FindRule(exeName);

        Assert.NotNull(rule);
        Assert.Equal("工作", rule!.Category);
        Assert.Equal("Microsoft Word", rule.DisplayName);
    }

    [Fact]
    public void FindRule_NoMatch_ReturnsNull()
    {
        Assert.Null(_service.FindRule("nonexistent_app.exe"));
        Assert.Null(_service.FindRule("unknown.exe"));
    }

    [Fact]
    public void FindRule_Empty_ReturnsNull()
    {
        Assert.Null(_service.FindRule(""));
    }

    [Fact]
    public void FindRule_Null_ReturnsNull()
    {
        Assert.Null(_service.FindRule(null!));
    }

    // ====================================================================
    //  SuggestCategory
    // ====================================================================

    [Theory]
    [InlineData("chrome.exe", "工作")]
    [InlineData("msedge.exe", "工作")]
    [InlineData("explorer.exe", "其他")]
    [InlineData("Code.exe", "工作")]
    [InlineData("steam.exe", "娱乐")]
    [InlineData("Discord.exe", "社交")]
    [InlineData("WeChat.exe", "社交")]
    [InlineData("spotify.exe", "娱乐")]
    [InlineData("notepad.exe", "其他")]
    [InlineData("OneNote.exe", "学习")]
    public void SuggestCategory_ReturnsCorrectCategory(string exeName, string expectedCategory)
    {
        Assert.Equal(expectedCategory, _service.SuggestCategory(exeName));
    }

    [Fact]
    public void SuggestCategory_NoMatch_ReturnsNull()
    {
        Assert.Null(_service.SuggestCategory("totally_unknown.exe"));
    }

    [Fact]
    public void SuggestCategory_IsCaseInsensitive()
    {
        Assert.Equal("社交", _service.SuggestCategory("wechat.exe"));
        Assert.Equal("社交", _service.SuggestCategory("WECHAT.EXE"));
    }

    // ====================================================================
    //  SuggestDisplayName
    // ====================================================================

    [Theory]
    [InlineData("chrome.exe", "Google Chrome")]
    [InlineData("Code.exe", "Visual Studio Code")]
    [InlineData("devenv.exe", "Visual Studio")]
    [InlineData("WeChat.exe", "微信")]
    [InlineData("QQ.exe", "QQ")]
    [InlineData("notepad.exe", "记事本")]
    public void SuggestDisplayName_ReturnsCorrectName(string exeName, string expectedName)
    {
        Assert.Equal(expectedName, _service.SuggestDisplayName(exeName));
    }

    [Fact]
    public void SuggestDisplayName_NoMatch_ReturnsNull()
    {
        Assert.Null(_service.SuggestDisplayName("mystery_app.exe"));
    }

    // ====================================================================
    //  GetAllRules
    // ====================================================================

    [Fact]
    public void GetAllRules_LoadsExpectedCount()
    {
        var rules = _service.GetAllRules();

        // The builtin_rules.json contains 20 rules
        Assert.Equal(20, rules.Count);
    }

    // ====================================================================
    //  JSON string constructor (alternative loading)
    // ====================================================================

    [Fact]
    public void Constructor_FromJsonString_ParsesRulesCorrectly()
    {
        var json = """
            {
              "rules": [
                { "exeName": "test.exe", "category": "测试", "displayName": "Test App" }
              ]
            }
            """;

        var service = new BuiltinRulesService(json);

        var rule = service.FindRule("TEST.EXE");
        Assert.NotNull(rule);
        Assert.Equal("测试", rule!.Category);
        Assert.Equal("Test App", rule.DisplayName);
    }

    [Fact]
    public void Constructor_FromJsonString_EmptyRules()
    {
        var json = """{ "rules": [] }""";

        var service = new BuiltinRulesService(json);

        Assert.Empty(service.GetAllRules());
        Assert.Null(service.FindRule("anything.exe"));
    }
}
