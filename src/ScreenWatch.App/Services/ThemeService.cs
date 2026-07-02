using System.Windows;

namespace ScreenWatch.App.Services;

/// <summary>
/// 主题切换服务：通过替换 Application.Resources.MergedDictionaries 中的主题资源字典，
/// 实现浅色/深色模式动态切换。主题偏好持久化到 SettingsRepository。
/// </summary>
public static class ThemeService
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    /// <summary>当前生效的主题名称</summary>
    public static string CurrentTheme { get; private set; } = Light;

    /// <summary>
    /// 应用指定主题，替换已加载的主题资源字典。
    /// </summary>
    public static void ApplyTheme(string themeName)
    {
        if (themeName != Dark)
            themeName = Light;

        var themeFile = themeName == Dark ? "DarkTheme" : "ModernTheme";
        var uri = new Uri($"pack://application:,,,/Themes/{themeFile}.xaml", UriKind.Absolute);

        var newDict = new ResourceDictionary { Source = uri };

        var app = Application.Current;
        if (app == null)
            return;

        var dictionaries = app.Resources.MergedDictionaries;

        // 移除已存在的主题字典（ModernTheme 或 DarkTheme）
        for (int i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source;
            if (source != null &&
                (source.OriginalString.Contains("ModernTheme") ||
                 source.OriginalString.Contains("DarkTheme")))
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Add(newDict);
        CurrentTheme = themeName;
    }

    /// <summary>
    /// 从设置仓库加载主题偏好并应用。在 ServiceHost.Initialize 之后调用。
    /// </summary>
    public static void LoadFromSettings()
    {
        var theme = ServiceHost.SettingsRepository.Get("theme", Light);
        ApplyTheme(theme);
    }

    /// <summary>
    /// 保存主题偏好到设置仓库并立即应用。
    /// </summary>
    public static void SaveAndApply(string themeName)
    {
        ServiceHost.SettingsRepository.Set("theme", themeName);
        ApplyTheme(themeName);
    }
}
