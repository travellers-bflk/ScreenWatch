using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenWatch.App.Services;
using ScreenWatch.Core.Models;

namespace ScreenWatch.App.ViewModels;

/// <summary>
/// 排除规则行数据（支持 UI 双向绑定）。
/// </summary>
public class ExclusionRuleRow : ObservableObject
{
    public int RuleId { get; init; }

    private string _matchType = "exe_name";
    public string MatchType
    {
        get => _matchType;
        set => SetProperty(ref _matchType, value);
    }

    private string _pattern = "";
    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }

    private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string MatchTypeDisplay => MatchType == "exe_name" ? "应用名" : "路径";
}

/// <summary>
/// 时段配置行数据（支持 UI 双向绑定，时间用字符串表示）。
/// </summary>
public class TimePeriodRow : ObservableObject
{
    public int PeriodId { get; init; }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _startTime = "09:00";
    public string StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }

    private string _endTime = "17:00";
    public string EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }

    private string _weekdays = "";
    public string Weekdays { get => _weekdays; set => SetProperty(ref _weekdays, value); }

    private bool _enabled = true;
    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }

    /// <summary>适用星期的人类可读描述</summary>
    public string WeekdaysDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Weekdays))
                return "每天";

            var days = Weekdays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var names = new Dictionary<int, string>
            {
                { 0, "日" }, { 1, "一" }, { 2, "二" }, { 3, "三" },
                { 4, "四" }, { 5, "五" }, { 6, "六" }
            };

            var labels = new List<string>();
            foreach (var d in days)
            {
                if (int.TryParse(d, out var v) && names.TryGetValue(v, out var n))
                    labels.Add($"周{n}");
            }
            return labels.Count > 0 ? string.Join("、", labels) : "每天";
        }
    }
}

/// <summary>
/// 设置页视图模型：通用设置、排除白名单管理、时段配置。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    // ----- 通用设置 -----
    [ObservableProperty] private bool _isAutoStartEnabled;
    [ObservableProperty] private int _idleThresholdSeconds = 300;
    [ObservableProperty] private int _backgroundScanIntervalSeconds = 45;
    [ObservableProperty] private bool _captureWindowTitle;
    [ObservableProperty] private bool _showPrivacyWarning;

    private bool _isLoading;

    // ----- 排除白名单 -----
    public ObservableCollection<ExclusionRuleRow> ExclusionRules { get; } = new();

    [ObservableProperty] private string _newExclusionPattern = "";
    [ObservableProperty] private string _newExclusionMatchType = "exe_name";

    // ----- 时段配置 -----
    public ObservableCollection<TimePeriodRow> TimePeriods { get; } = new();

    // 新增时段的默认值
    [ObservableProperty] private string _newPeriodName = "";
    [ObservableProperty] private string _newPeriodStart = "09:00";
    [ObservableProperty] private string _newPeriodEnd = "17:00";
    [ObservableProperty] private string _newPeriodWeekdays = "";

    /// <summary>
    /// 加载所有设置数据。
    /// </summary>
    public void LoadData()
    {
        _isLoading = true;

        // 通用设置
        IsAutoStartEnabled = ServiceHost.AutoStartService.IsAutoStartEnabled();
        IdleThresholdSeconds = ServiceHost.SettingsRepository.Get("idle_threshold_seconds", 300);
        BackgroundScanIntervalSeconds = ServiceHost.SettingsRepository.Get("background_scan_interval_seconds", 45);
        CaptureWindowTitle = ServiceHost.SettingsRepository.Get("capture_window_title", false);
        ShowPrivacyWarning = CaptureWindowTitle;

        _isLoading = false;

        // 排除白名单
        LoadExclusions();

        // 时段配置
        LoadTimePeriods();
    }

    private void LoadExclusions()
    {
        ExclusionRules.Clear();
        foreach (var rule in ServiceHost.ExclusionRepository.GetAll())
        {
            ExclusionRules.Add(new ExclusionRuleRow
            {
                RuleId = rule.RuleId,
                MatchType = rule.MatchType,
                Pattern = rule.Pattern,
                Enabled = rule.Enabled
            });
        }
    }

    private void LoadTimePeriods()
    {
        TimePeriods.Clear();
        foreach (var period in ServiceHost.TimePeriodRepository.GetAll())
        {
            TimePeriods.Add(new TimePeriodRow
            {
                PeriodId = period.PeriodId,
                Name = period.Name,
                StartTime = period.StartTime.ToString("HH:mm"),
                EndTime = period.EndTime.ToString("HH:mm"),
                Weekdays = period.Weekdays,
                Enabled = period.Enabled
            });
        }
    }

    // ================================================================
    //  通用设置命令
    // ================================================================

    /// <summary>
    /// 切换开机自启动。
    /// </summary>
    [RelayCommand]
    private void ToggleAutoStart()
    {
        if (IsAutoStartEnabled)
            ServiceHost.AutoStartService.EnableAutoStart();
        else
            ServiceHost.AutoStartService.DisableAutoStart();
    }

    /// <summary>
    /// 保存通用设置（闲置阈值、扫描间隔、窗口标题采集）。
    /// </summary>
    [RelayCommand]
    private void SaveSettings()
    {
        ServiceHost.SettingsRepository.Set("idle_threshold_seconds", IdleThresholdSeconds.ToString());
        ServiceHost.SettingsRepository.Set("background_scan_interval_seconds", BackgroundScanIntervalSeconds.ToString());
        ServiceHost.SettingsRepository.Set("capture_window_title", CaptureWindowTitle.ToString());
        ShowPrivacyWarning = CaptureWindowTitle;
    }

    /// <summary>
    /// 当采集窗口标题勾选状态变化时，显示隐私警告。
    /// </summary>
    partial void OnCaptureWindowTitleChanged(bool value)
    {
        if (_isLoading) return;

        if (value)
        {
            ShowPrivacyWarning = true;
            MessageBox.Show(
                "警告：启用窗口标题采集后，ScreenWatch 将记录您使用的每个窗口的标题文本。\n" +
                "这可能包含敏感信息（如邮件主题、文档名称、网页标题等）。\n" +
                "数据仅存储在本地，不会上传到任何服务器。\n\n" +
                "请确认您了解此风险后再启用。",
                "隐私警告",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        else
        {
            ShowPrivacyWarning = false;
        }
    }

    // ================================================================
    //  排除白名单命令
    // ================================================================

    /// <summary>
    /// 添加排除规则。
    /// </summary>
    [RelayCommand]
    private void AddExclusion()
    {
        if (string.IsNullOrWhiteSpace(NewExclusionPattern))
            return;

        ServiceHost.ExclusionRepository.Add(NewExclusionMatchType, NewExclusionPattern.Trim());
        NewExclusionPattern = "";
        LoadExclusions();
    }

    /// <summary>
    /// 删除排除规则。
    /// </summary>
    [RelayCommand]
    private void RemoveExclusion(ExclusionRuleRow row)
    {
        if (row == null) return;
        ServiceHost.ExclusionRepository.Remove(row.RuleId);
        LoadExclusions();
    }

    /// <summary>
    /// 切换排除规则启用状态。
    /// </summary>
    [RelayCommand]
    private void ToggleExclusion(ExclusionRuleRow row)
    {
        if (row == null) return;
        ServiceHost.ExclusionRepository.Toggle(row.RuleId, row.Enabled);
    }

    // ================================================================
    //  时段配置命令
    // ================================================================

    /// <summary>
    /// 添加时段。
    /// </summary>
    [RelayCommand]
    private void AddTimePeriod()
    {
        if (string.IsNullOrWhiteSpace(NewPeriodName))
        {
            MessageBox.Show("请输入时段名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TimeOnly.TryParse(NewPeriodStart, out var startTime) ||
            !TimeOnly.TryParse(NewPeriodEnd, out var endTime))
        {
            MessageBox.Show("时间格式不正确，请使用 HH:mm 格式（如 09:00）", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var period = new TimePeriod
        {
            Name = NewPeriodName.Trim(),
            StartTime = startTime,
            EndTime = endTime,
            Weekdays = string.IsNullOrWhiteSpace(NewPeriodWeekdays) ? "" : NewPeriodWeekdays.Trim(),
            Enabled = true
        };

        ServiceHost.TimePeriodRepository.Add(period);
        NewPeriodName = "";
        NewPeriodStart = "09:00";
        NewPeriodEnd = "17:00";
        NewPeriodWeekdays = "";
        LoadTimePeriods();
    }

    /// <summary>
    /// 保存时段修改。
    /// </summary>
    [RelayCommand]
    private void SaveTimePeriod(TimePeriodRow row)
    {
        if (row == null) return;

        if (!TimeOnly.TryParse(row.StartTime, out var startTime) ||
            !TimeOnly.TryParse(row.EndTime, out var endTime))
        {
            MessageBox.Show("时间格式不正确，请使用 HH:mm 格式", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var period = new TimePeriod
        {
            PeriodId = row.PeriodId,
            Name = row.Name,
            StartTime = startTime,
            EndTime = endTime,
            Weekdays = row.Weekdays ?? "",
            Enabled = row.Enabled
        };

        ServiceHost.TimePeriodRepository.Update(period);
        LoadTimePeriods();
    }

    /// <summary>
    /// 删除时段。
    /// </summary>
    [RelayCommand]
    private void DeleteTimePeriod(TimePeriodRow row)
    {
        if (row == null) return;
        ServiceHost.TimePeriodRepository.Delete(row.PeriodId);
        LoadTimePeriods();
    }
}
