using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenWatch.App.Controls;

/// <summary>
/// 简化版柱状图占位控件：使用 <see cref="Border"/> 模拟柱形，
/// 通过 <see cref="Values"/> 依赖属性绑定数据；未提供数据时显示示例柱形。
/// 柱形颜色取自主题资源 PrimaryBrush，深浅色模式下自动适配。
/// </summary>
public partial class ChartPlaceholder : UserControl
{
    /// <summary>
    /// 数据源依赖属性：可绑定任意数值集合（double）。
    /// </summary>
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values),
            typeof(IEnumerable),
            typeof(ChartPlaceholder),
            new PropertyMetadata(null, OnValuesChanged));

    /// <summary>要绘制的数值集合。</summary>
    public IEnumerable Values
    {
        get => (IEnumerable)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public ChartPlaceholder()
    {
        InitializeComponent();
        SizeChanged += (_, _) => DrawChart();
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChartPlaceholder chart)
            chart.DrawChart();
    }

    /// <summary>
    /// 根据当前 <see cref="Values"/> 与控件尺寸绘制柱状图。
    /// </summary>
    private void DrawChart()
    {
        var canvas = ChartCanvas;
        if (canvas == null)
            return;

        canvas.Children.Clear();

        // 收集数值；未绑定时使用示例数据，便于设计预览
        var values = new List<double>();
        if (Values != null)
        {
            foreach (var item in Values)
            {
                if (item is IConvertible convertible)
                {
                    if (convertible.ToDouble(null) is var d && !double.IsNaN(d))
                        values.Add(d);
                }
            }
        }

        if (values.Count == 0)
            values = new List<double> { 30, 45, 20, 60, 35, 50, 25 };

        double width = ActualWidth > 0 ? ActualWidth : 400;
        double height = ActualHeight > 0 ? ActualHeight : 200;
        double padding = 10;
        double availableWidth = Math.Max(1, width - padding * 2);
        double availableHeight = Math.Max(1, height - padding * 2);

        int count = values.Count;
        double gap = 8;
        double barWidth = Math.Max(6, (availableWidth - gap * (count - 1)) / count);
        double maxVal = values.Max();

        if (maxVal <= 0)
            maxVal = 1;

        var brush = TryFindResource("PrimaryBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF));

        for (int i = 0; i < count; i++)
        {
            double val = values[i];
            double barHeight = availableHeight * (val / maxVal);

            // 跳过零值，不绘制柱形以保持严格比例
            if (barHeight <= 0)
                continue;

            double x = padding + i * (barWidth + gap);
            double y = height - padding - barHeight;

            var bar = new Border
            {
                Width = barWidth,
                Height = barHeight,
                Background = brush,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
            };

            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            canvas.Children.Add(bar);
        }
    }
}
