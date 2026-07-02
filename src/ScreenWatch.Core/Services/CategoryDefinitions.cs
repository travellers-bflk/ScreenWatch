namespace ScreenWatch.Core.Services;

/// <summary>
/// Static metadata for the nine built-in application categories, including
/// display name, emoji icon, colour, and keyword lists used by the
/// <see cref="AutoCategorize"/> keyword-matching algorithm.
/// </summary>
public static class CategoryDefinitions
{
    /// <summary>
    /// Definition of a single built-in category.
    /// </summary>
    public sealed class Definition
    {
        public string Name { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public string Color { get; init; } = string.Empty;
        public string[] Keywords { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// The nine built-in categories in matching-priority order.
    /// "其他" (Other) is always last and acts as the fallback.
    /// </summary>
    public static readonly Definition[] Definitions =
    {
        new()
        {
            Name = "工作", Icon = "💼", Color = "#4A90D9",
            Keywords = new[]
            {
                "office", "word", "excel", "powerpoint", "outlook", "teams",
                "slack", "zoom", "wps", "钉钉", "企业微信", "飞书", "winword", "excel.exe", "powerpnt"
            }
        },
        new()
        {
            Name = "社交", Icon = "💬", Color = "#F39C12",
            Keywords = new[]
            {
                "wechat", "qq", "weibo", "twitter", "facebook", "instagram",
                "whatsapp", "telegram", "discord", "微信", "微博", "知乎", "小红书"
            }
        },
        new()
        {
            Name = "娱乐", Icon = "🎮", Color = "#E74C3C",
            Keywords = new[]
            {
                "bilibili", "douyin", "tiktok", "netflix", "youtube",
                "twitch", "hulu", "爱奇艺", "腾讯视频", "优酷", "芒果tv"
            }
        },
        new()
        {
            Name = "开发", Icon = "💻", Color = "#9B59B6",
            Keywords = new[]
            {
                "vscode", "visual studio", "intellij", "pycharm", "webstorm",
                "eclipse", "xcode", "android studio", "git", "github", "docker",
                "terminal", "cmd", "powershell", "devenv", "code.exe", "rider",
                "goland", "clion", "rustrover"
            }
        },
        new()
        {
            Name = "学习", Icon = "📚", Color = "#2ECC71",
            Keywords = new[]
            {
                "chrome", "edge", "firefox", "safari", "browser",
                "coursera", "udemy", "khan academy", "duolingo", "anki",
                "notion", "evernote", "onenote", "msedge"
            }
        },
        new()
        {
            Name = "工具", Icon = "🔧", Color = "#3498DB",
            Keywords = new[]
            {
                "explorer", "file manager", "calculator", "notepad", "paint",
                "photoshop", "illustrator", "premiere", "after effects",
                "blender", "autocad", "solidworks", "acad", "snipping", "7-zip",
                "winrar", "taskmgr"
            }
        },
        new()
        {
            Name = "媒体", Icon = "🎬", Color = "#E67E22",
            Keywords = new[]
            {
                "vlc", "potplayer", "kmplayer", "itunes", "spotify",
                "apple music", "qq音乐", "网易云音乐", "酷狗音乐", "虾米音乐",
                "qqmusic", "netease", "kugou", "foobar", "aimp"
            }
        },
        new()
        {
            Name = "游戏", Icon = "🕹️", Color = "#E91E63",
            Keywords = new[]
            {
                "steam", "epic", "origin", "uplay", "battle.net",
                "minecraft", "league of legends", "dota", "cs:go", "fortnite",
                "pubg", "原神", "王者荣耀", "和平精英", "genshin"
            }
        },
        new()
        {
            Name = "其他", Icon = "📦", Color = "#95A5A6",
            Keywords = Array.Empty<string>()
        }
    };

    /// <summary>
    /// Looks up a definition by category name (case-insensitive).
    /// Returns null if the name does not match any built-in category.
    /// </summary>
    public static Definition? FindByName(string name)
    {
        return Definitions.FirstOrDefault(
            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Keyword-based auto-classification. Concatenates the app's display name,
    /// exe name, and exe path (all lower-cased) and returns the first category
    /// whose keyword list contains a match. Falls back to "其他" when nothing
    /// matches.
    /// </summary>
    /// <param name="displayName">The app's friendly display name.</param>
    /// <param name="exeName">The app's exe file name (e.g. "chrome.exe").</param>
    /// <param name="exePath">The app's full exe path (may be null).</param>
    /// <returns>The matched category name, or "其他" if no keyword matched.</returns>
    public static string AutoCategorize(string displayName, string exeName, string? exePath)
    {
        var combined = $"{displayName ?? ""} {exeName ?? ""} {exePath ?? ""}".ToLowerInvariant();

        foreach (var def in Definitions)
        {
            // "其他" is the fallback — skip it in the matching loop
            if (def.Keywords.Length == 0)
                continue;

            foreach (var keyword in def.Keywords)
            {
                if (combined.Contains(keyword.ToLowerInvariant()))
                    return def.Name;
            }
        }

        return "其他";
    }
}
