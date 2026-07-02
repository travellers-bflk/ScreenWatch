using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ScreenWatch.App.Helpers;

namespace ScreenWatch.App.Converters;

/// <summary>
/// 将秒数转换为中文时长格式（"X小时Y分钟" / "Y分钟"）。
/// </summary>
public class DurationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int seconds)
            return TimeFormatter.FormatDuration(seconds);
        return "0分钟";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 反转布尔值。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}

/// <summary>
/// 布尔值转可见性：true→Visible, false→Collapsed。
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is bool b && b;
        // 反转模式：参数为 "Inverse" 时反转
        if (parameter is string s && s == "Inverse")
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is Visibility v && v == Visibility.Visible;
        if (parameter is string s && s == "Inverse")
            visible = !visible;
        return visible;
    }
}

/// <summary>
/// 空值/零值转可见性：null 或空集合→Collapsed，否则→Visible。
/// 参数 "Inverse" 时反转（空→Visible，非空→Collapsed）。
/// </summary>
public class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasData = value switch
        {
            null => false,
            int i => i > 0,
            System.Collections.ICollection c => c.Count > 0,
            System.Collections.IEnumerable e => e.GetEnumerator().MoveNext(),
            _ => true
        };

        if (parameter is string s && s == "Inverse")
            hasData = !hasData;

        return hasData ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 计算条形图宽度：value/max * maxWidth。
/// 单值转换器，maxWidth 通过 ConverterParameter 传入。
/// </summary>
public class BarWidthConverter : IValueConverter
{
    /// <summary>
    /// value = 当前秒数, parameter = "maxSeconds|maxWidth"
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int seconds)
            return 0.0;

        var parts = parameter?.ToString()?.Split('|');
        if (parts is { Length: 2 } &&
            double.TryParse(parts[0], out var max) &&
            double.TryParse(parts[1], out var maxWidth))
        {
            if (max <= 0) return 0.0;
            return Math.Min(maxWidth, seconds / max * maxWidth);
        }

        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 将颜色 hex 字符串转换为 SolidColorBrush。
/// </summary>
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(s);
                return new SolidColorBrush(color);
            }
            catch { }
        }
        return new SolidColorBrush(Colors.SteelBlue);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
