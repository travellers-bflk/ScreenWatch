using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenWatch.App.Helpers;
using ScreenWatch.App.Services;
using ScreenWatch.Core.Models;

namespace ScreenWatch.App.ViewModels;

/// <summary>
/// 应用列表行数据：合并 AppInfo 与使用统计。
/// </summary>
public class AppListRow : ObservableObject
{
    public int AppId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ExeName { get; init; } = string.Empty;
    public string ExePath { get; init; } = string.Empty;
    public bool IsUnrecognized { get; init; }

    private int? _categoryId;
    public int? CategoryId
    {
        get => _categoryId;
        set => SetProperty(ref _categoryId, value);
    }

    private BitmapImage? _icon;
    /// <summary>应用图标（异步加载，失败时为 null）</summary>
    public BitmapImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public int ForegroundSeconds { get; init; }
    public int BackgroundSeconds { get; init; }
    public int TotalSeconds => ForegroundSeconds + BackgroundSeconds;
    public string ForegroundDuration => TimeFormatter.FormatDuration(ForegroundSeconds);
    public string BackgroundDuration => TimeFormatter.FormatDuration(BackgroundSeconds);
    public string TotalDuration => TimeFormatter.FormatDuration(TotalSeconds);
}

/// <summary>
/// 应用列表页视图模型：展示所有应用及其使用时长，支持修改分类。
/// </summary>
public partial class AppListViewModel : ObservableObject, IDisposable
{
    public ObservableCollection<AppListRow> AppRows { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private int _unrecognizedCount;

    /// <summary>最近一次加载使用的日期范围（用于自动分类/重置后重新加载）</summary>
    private DateTime _lastStart = DateTime.Today;
    private DateTime _lastEnd = DateTime.Today;

    // ===== 图标加载 =====

    private readonly AppIconExtractor _iconExtractor = AppIconExtractor.Instance;
    /// <summary>ViewModel 级图标缓存（快速同步路径，避免重复进入异步提取流程）</summary>
    private readonly Dictionary<string, BitmapImage> _iconCache = new();
    /// <summary>取消令牌：用户快速切换日期范围时取消未完成的图标加载</summary>
    private CancellationTokenSource? _iconCts;
    /// <summary>应用数超过此阈值时仅预加载前 N 项图标，其余延迟加载</summary>
    private const int PreloadThreshold = 50;
    /// <summary>预加载的图标数量</summary>
    private const int MaxPreloadCount = 20;

    /// <summary>
    /// 加载应用列表数据，合并使用时长统计。数据同步加载完成后异步加载图标。
    /// </summary>
    public void LoadData(DateTime start, DateTime end)
    {
        _lastStart = start;
        _lastEnd = end;

        // 取消上一次未完成的图标加载（用户快速切换日期范围时）
        _iconCts?.Cancel();
        _iconCts = new CancellationTokenSource();
        var token = _iconCts.Token;

        // 加载分类列表
        Categories.Clear();
        foreach (var cat in ServiceHost.CategoryRepository.GetAll())
            Categories.Add(cat);

        // 加载所有应用
        var allApps = ServiceHost.AppRepository.GetAllApps();
        var rangeEnd = end.Date.AddDays(1).AddTicks(-1);
        var appStats = ServiceHost.UsageQueryService.GetAppStatsByRange(start, rangeEnd);
        var statsByAppId = appStats.ToDictionary(s => s.AppId);

        AppRows.Clear();
        UnrecognizedCount = 0;

        foreach (var app in allApps)
        {
            var hasStats = statsByAppId.TryGetValue(app.AppId, out var stat);
            var isUnrecognized = !app.IsRecognized;
            if (isUnrecognized) UnrecognizedCount++;

            AppRows.Add(new AppListRow
            {
                AppId = app.AppId,
                DisplayName = string.IsNullOrEmpty(app.DisplayName) ? app.ExeName : app.DisplayName,
                ExeName = app.ExeName,
                ExePath = app.ExePath,
                CategoryId = app.CategoryId,
                IsUnrecognized = isUnrecognized,
                ForegroundSeconds = hasStats ? stat!.ForegroundSeconds : 0,
                BackgroundSeconds = hasStats ? stat!.BackgroundSeconds : 0,
            });
        }

        HasData = AppRows.Count > 0;

        // 异步加载图标（不阻塞 UI 线程）
        _ = LoadAppIconsAsync(token);
    }

    /// <summary>
    /// 批量异步加载应用图标。应用数超过阈值时仅预加载前 N 项，其余延迟加载。
    /// </summary>
    private async Task LoadAppIconsAsync(CancellationToken token)
    {
        var rows = AppRows.ToList();
        if (rows.Count == 0) return;

        // 批量预加载：超过阈值时仅加载前 MaxPreloadCount 项
        var preloadCount = rows.Count > PreloadThreshold ? MaxPreloadCount : rows.Count;

        for (var i = 0; i < preloadCount; i++)
        {
            if (token.IsCancellationRequested) return;
            await LoadAppIconAsync(rows[i], token);
        }

        // 延迟加载剩余项
        if (rows.Count > PreloadThreshold)
        {
            for (var i = MaxPreloadCount; i < rows.Count; i++)
            {
                if (token.IsCancellationRequested) return;
                await LoadAppIconAsync(rows[i], token);
            }
        }
    }

    /// <summary>
    /// 异步加载单个应用图标。优先读取 ViewModel 缓存，未命中则调用 AppIconExtractor 提取。
    /// AppIconExtractor 内部已有：内存缓存 → 磁盘缓存 → Win32 提取 → 默认图标降级。
    /// </summary>
    private async Task LoadAppIconAsync(AppListRow row, CancellationToken token)
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
            var icon = await _iconExtractor.ExtractIconAsync(exePath, 32);

            if (token.IsCancellationRequested) return;

            _iconCache[exePath] = icon;
            row.Icon = icon;
        }
        catch (Exception ex)
        {
            // 网络路径或非法路径：图标保持 null，UI 显示空白占位
            Debug.WriteLine($"[AppListViewModel] 图标加载失败 {row.ExePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// 一键保存单个应用的分类选择（由 ComboBox SelectionChanged 触发，无需点“应用”按钮）。
    /// </summary>
    public void SaveAppCategory(AppListRow row)
    {
        if (row.CategoryId.HasValue)
        {
            ServiceHost.AppRepository.UpdateCategory(row.AppId, row.CategoryId.Value);
            // 更新未归类计数
            if (row.IsUnrecognized)
                UnrecognizedCount = Math.Max(0, UnrecognizedCount - 1);
        }
    }

    /// <summary>
    /// 对所有未归类应用执行关键词自动分类。返回被分类的应用数量。
    /// </summary>
    public int ExecuteAutoCategorizeAll()
    {
        var count = ServiceHost.ClassificationService.AutoCategorizeAllApps();
        LoadData(_lastStart, _lastEnd);
        return count;
    }

    /// <summary>
    /// 重置全部分类并重新自动分类。返回被分类的应用数量。
    /// </summary>
    public int ExecuteResetAllCategories()
    {
        var count = ServiceHost.ClassificationService.ResetAllCategories();
        LoadData(_lastStart, _lastEnd);
        return count;
    }

    /// <summary>
    /// 释放资源：取消未完成的图标加载，清理 ViewModel 级缓存。
    /// 注意：不释放 _iconExtractor（单例，由应用生命周期管理）。
    /// </summary>
    public void Dispose()
    {
        _iconCts?.Cancel();
        _iconCts?.Dispose();
        _iconCts = null;
        _iconCache.Clear();
    }
}
