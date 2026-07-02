using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ScreenWatch.App.Helpers;
using ScreenWatch.App.Services;
using ScreenWatch.Core.Models;

namespace ScreenWatch.App.ViewModels;

// ================================================================
//  条形图行数据模型
// ================================================================

/// <summary>
/// 应用排行条形图行数据。
/// </summary>
public class AppBarRow : ObservableObject
{
    public int AppId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ExeName { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int ForegroundSeconds { get; set; }
    public int BackgroundSeconds { get; set; }
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;
    public string ForegroundDuration => TimeFormatter.FormatDuration(ForegroundSeconds);
    public string BackgroundDuration => TimeFormatter.FormatDuration(BackgroundSeconds);
    public string TotalDuration => TimeFormatter.FormatDuration(TotalSeconds);
    /// <summary>前台时长条宽度（像素）</summary>
    public double ForegroundBarWidth { get; set; }
    /// <summary>后台时长条宽度（像素）</summary>
    public double BackgroundBarWidth { get; set; }

    private BitmapImage? _icon;
    /// <summary>应用图标（异步加载，失败时为 null）</summary>
    public BitmapImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }
}

/// <summary>
/// 分类汇总条形图行数据。
/// </summary>
public class CategoryBarRow
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int ForegroundSeconds { get; set; }
    public int BackgroundSeconds { get; set; }
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;
    public string TotalDuration => TimeFormatter.FormatDuration(TotalSeconds);
    public string ForegroundDuration => TimeFormatter.FormatDuration(ForegroundSeconds);
    public double BarWidth { get; set; }
    public int AppCount { get; set; }
    /// <summary>占比百分比文本</summary>
    public string PercentageText { get; set; } = string.Empty;

    /// <summary>将颜色 hex 字符串转换为 SolidColorBrush</summary>
    public SolidColorBrush ColorBrush
    {
        get
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(
                    string.IsNullOrEmpty(Color) ? "#2196F3" : Color);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.SteelBlue);
            }
        }
    }
}

/// <summary>
/// 每日趋势柱状图行数据。
/// </summary>
public class DailyBarRow
{
    public DateTime Date { get; set; }
    public string DateLabel => Date.ToString("M/d");
    public string WeekdayLabel => Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        DayOfWeek.Sunday => "周日",
        _ => ""
    };
    public int TotalSeconds { get; set; }
    public string TotalDuration => TimeFormatter.FormatDuration(TotalSeconds);
    /// <summary>柱状图高度（像素）</summary>
    public double BarHeight { get; set; }
    public bool IsToday => Date.Date == DateTime.Today;
}

/// <summary>
/// 时段分析条形图行数据。
/// </summary>
public class PeriodBarRow
{
    public string PeriodName { get; set; } = string.Empty;
    public int ForegroundSeconds { get; set; }
    public int BackgroundSeconds { get; set; }
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;
    public string ForegroundDuration => TimeFormatter.FormatDuration(ForegroundSeconds);
    public string TotalDuration => TimeFormatter.FormatDuration(TotalSeconds);
    public double ForegroundBarWidth { get; set; }
    public double BackgroundBarWidth { get; set; }
}

// ================================================================
//  DashboardViewModel
// ================================================================

/// <summary>
/// 统计概览页视图模型：汇总卡片、应用排行、分类汇总、每日趋势、时段分析。
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    /// <summary>条形图最大宽度（像素）</summary>
    private const double MaxBarWidth = 500;
    /// <summary>柱状图最大高度（像素）</summary>
    private const double MaxBarHeight = 160;

    // ----- 汇总卡片 -----
    [ObservableProperty] private int _totalForegroundSeconds;
    [ObservableProperty] private int _totalBackgroundSeconds;
    [ObservableProperty] private int _activeAppCount;

    public string TotalForegroundDuration => TimeFormatter.FormatDuration(TotalForegroundSeconds);
    public string TotalBackgroundDuration => TimeFormatter.FormatDuration(TotalBackgroundSeconds);

    // ----- 空状态 -----
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private bool _hasPeriodData;

    // ----- 条形图集合 -----
    public ObservableCollection<AppBarRow> AppBars { get; } = new();
    public ObservableCollection<CategoryBarRow> CategoryBars { get; } = new();
    public ObservableCollection<DailyBarRow> DailyBars { get; } = new();
    public ObservableCollection<PeriodBarRow> PeriodBars { get; } = new();

    // ===== 图标加载 =====

    private readonly AppIconExtractor _iconExtractor = AppIconExtractor.Instance;
    /// <summary>ViewModel 级图标缓存（快速同步路径，避免重复进入异步提取流程）</summary>
    private readonly Dictionary<string, BitmapImage> _iconCache = new();
    /// <summary>取消令牌：用户快速切换日期范围时取消未完成的图标加载</summary>
    private CancellationTokenSource? _iconCts;

    /// <summary>
    /// 加载指定日期范围内的统计数据。
    /// </summary>
    public void LoadData(DateTime start, DateTime end)
    {
        var query = ServiceHost.UsageQueryService;
        // 确保结束日期包含当天全天
        var rangeEnd = end.Date.AddDays(1).AddTicks(-1);

        var appStats = query.GetAppStatsByRange(start, rangeEnd);
        var categoryStats = query.GetCategoryStatsByRange(start, rangeEnd);
        var dailyStats = query.GetDailyStats(start, end);
        var periodStats = query.GetTimePeriodStats(start);

        // 汇总卡片
        TotalForegroundSeconds = appStats.Sum(a => a.ForegroundSeconds);
        TotalBackgroundSeconds = appStats.Sum(a => a.BackgroundSeconds);
        ActiveAppCount = appStats.Count;
        HasData = appStats.Count > 0;

        // 应用排行（最多显示前 20 个，按总时长降序排列）
        AppBars.Clear();
        var appInfoById = ServiceHost.AppRepository.GetAllApps().ToDictionary(a => a.AppId);
        // 先按总时长降序排序再取前 20 个，确保排行顺序与柱状图比例一致；
        // 最大值从已显示列表中计算，保证最长柱形恰好为 MaxBarWidth
        var topApps = appStats
            .OrderByDescending(a => a.TotalSeconds)
            .Take(20)
            .ToList();
        var maxAppTotal = topApps.Count > 0 ? topApps.Max(a => a.TotalSeconds) : 0;
        foreach (var stat in topApps)
        {
            AppBars.Add(new AppBarRow
            {
                AppId = stat.AppId,
                DisplayName = string.IsNullOrEmpty(stat.DisplayName) ? stat.ExeName : stat.DisplayName,
                ExeName = stat.ExeName,
                ExePath = appInfoById.TryGetValue(stat.AppId, out var appInfo) ? appInfo.ExePath : string.Empty,
                CategoryName = stat.CategoryName,
                ForegroundSeconds = stat.ForegroundSeconds,
                BackgroundSeconds = stat.BackgroundSeconds,
                ForegroundBarWidth = maxAppTotal > 0
                    ? stat.ForegroundSeconds / (double)maxAppTotal * MaxBarWidth
                    : 0,
                BackgroundBarWidth = maxAppTotal > 0
                    ? stat.BackgroundSeconds / (double)maxAppTotal * MaxBarWidth
                    : 0
            });
        }

        // 分类汇总
        CategoryBars.Clear();
        var grandTotal = categoryStats.Sum(c => c.TotalSeconds);
        var maxCategoryTotal = categoryStats.Count > 0 ? categoryStats.Max(c => c.TotalSeconds) : 0;
        foreach (var cat in categoryStats)
        {
            CategoryBars.Add(new CategoryBarRow
            {
                CategoryName = cat.CategoryName,
                Color = cat.Color,
                ForegroundSeconds = cat.ForegroundSeconds,
                BackgroundSeconds = cat.BackgroundSeconds,
                AppCount = cat.Apps.Count,
                BarWidth = maxCategoryTotal > 0
                    ? cat.TotalSeconds / (double)maxCategoryTotal * MaxBarWidth
                    : 0,
                PercentageText = grandTotal > 0
                    ? $"{cat.TotalSeconds * 100.0 / grandTotal:F1}%"
                    : "0%"
            });
        }

        // 每日趋势
        DailyBars.Clear();
        var maxDailyTotal = dailyStats.Count > 0 ? dailyStats.Max(d => d.TotalSeconds) : 0;
        foreach (var day in dailyStats)
        {
            DailyBars.Add(new DailyBarRow
            {
                Date = day.Date,
                TotalSeconds = day.TotalSeconds,
                BarHeight = maxDailyTotal > 0
                    ? day.TotalSeconds / (double)maxDailyTotal * MaxBarHeight
                    : 0
            });
        }

        // 时段分析
        PeriodBars.Clear();
        HasPeriodData = periodStats.Count > 0;
        var maxPeriodTotal = periodStats.Count > 0 ? periodStats.Max(p => p.TotalSeconds) : 0;
        foreach (var period in periodStats)
        {
            PeriodBars.Add(new PeriodBarRow
            {
                PeriodName = period.PeriodName,
                ForegroundSeconds = period.ForegroundSeconds,
                BackgroundSeconds = period.BackgroundSeconds,
                ForegroundBarWidth = maxPeriodTotal > 0
                    ? period.ForegroundSeconds / (double)maxPeriodTotal * MaxBarWidth
                    : 0,
                BackgroundBarWidth = maxPeriodTotal > 0
                    ? period.BackgroundSeconds / (double)maxPeriodTotal * MaxBarWidth
                    : 0
            });
        }

        // 异步加载应用排行图标（不阻塞 UI 线程）
        _iconCts?.Cancel();
        _iconCts = new CancellationTokenSource();
        _ = LoadAppIconsAsync(_iconCts.Token);

        // 手动触发属性变更通知（用于计算属性）
        OnPropertyChanged(nameof(TotalForegroundDuration));
        OnPropertyChanged(nameof(TotalBackgroundDuration));
    }

    // ===== 图标加载 =====

    /// <summary>
    /// 批量异步加载应用排行图标。
    /// </summary>
    private async Task LoadAppIconsAsync(CancellationToken token)
    {
        var rows = AppBars.ToList();
        foreach (var row in rows)
        {
            if (token.IsCancellationRequested) return;
            await LoadAppIconAsync(row, token);
        }
    }

    /// <summary>
    /// 异步加载单个应用图标。优先读取 ViewModel 缓存，未命中则调用 AppIconExtractor 提取。
    /// </summary>
    private async Task LoadAppIconAsync(AppBarRow row, CancellationToken token)
    {
        try
        {
            var exePath = row.ExePath;

            // 空路径：跳过提取，图标保持 null
            if (string.IsNullOrEmpty(exePath))
                return;

            // ViewModel 缓存：快速同步路径
            if (_iconCache.TryGetValue(exePath, out var cachedIcon))
            {
                row.Icon = cachedIcon;
                return;
            }

            // 调用 AppIconExtractor（内部有内存缓存 + 磁盘缓存 + Win32 提取 + 默认图标降级）
            var icon = await _iconExtractor.ExtractIconAsync(exePath, 24);

            if (token.IsCancellationRequested) return;

            _iconCache[exePath] = icon;
            row.Icon = icon;
        }
        catch (Exception ex)
        {
            // 网络路径或非法路径：图标保持 null，UI 显示空白占位
            Debug.WriteLine($"[DashboardViewModel] 图标加载失败 {row.ExePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// 释放资源：取消未完成的图标加载，清理 ViewModel 级缓存。
    /// </summary>
    public void Dispose()
    {
        _iconCts?.Cancel();
        _iconCts?.Dispose();
        _iconCts = null;
        _iconCache.Clear();
    }
}
