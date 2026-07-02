namespace ScreenWatch.App.Helpers;

/// <summary>
/// 时长格式化工具，将秒数转换为中文可读格式。
/// </summary>
public static class TimeFormatter
{
    /// <summary>
    /// 格式化时长：大于1小时显示"X小时Y分钟"，小于1小时显示"Y分钟"。
    /// </summary>
    public static string FormatDuration(int seconds)
    {
        if (seconds <= 0)
            return "0分钟";

        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;

        if (hours > 0)
            return $"{hours}小时{minutes}分钟";

        if (minutes > 0)
            return $"{minutes}分钟";

        return $"{seconds}秒";
    }

    /// <summary>
    /// 格式化时长（简短版）：大于1小时显示"Xh Ym"，小于1小时显示"Ym"。
    /// </summary>
    public static string FormatDurationShort(int seconds)
    {
        if (seconds <= 0)
            return "0m";

        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;

        if (hours > 0)
            return $"{hours}h{minutes}m";

        return $"{minutes}m";
    }
}
