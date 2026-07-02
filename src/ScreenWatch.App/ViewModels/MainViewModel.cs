using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScreenWatch.App.ViewModels;

/// <summary>
/// 主窗口视图模型：管理 Tab 切换、日期范围选择和统计数据刷新。
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>统计概览页</summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>应用列表页</summary>
    public AppListViewModel AppList { get; }

    /// <summary>设置页</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>分类管理页</summary>
    public CategoryViewModel Category { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    /// <summary>当前选中的统计周期名称（今天/本周/本月/自定义）</summary>
    [ObservableProperty]
    private string _selectedPeriodName = "今天";

    private bool _isLoading;

    public MainViewModel()
    {
        Dashboard = new DashboardViewModel();
        AppList = new AppListViewModel();
        Settings = new SettingsViewModel();
        Category = new CategoryViewModel();
    }

    /// <summary>
    /// 初始化所有子视图模型的数据。在窗口加载后调用。
    /// </summary>
    public void Initialize()
    {
        RefreshDashboard();
    }

    partial void OnStartDateChanged(DateTime value)
    {
        if (!_isLoading)
            RefreshDashboard();
    }

    partial void OnEndDateChanged(DateTime value)
    {
        if (!_isLoading)
            RefreshDashboard();
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        // 懒加载：切换到对应 Tab 时加载数据
        switch (value)
        {
            case 0: // 概览
                RefreshDashboard();
                break;
            case 1: // 应用列表
                AppList.LoadData(StartDate, EndDate);
                break;
            case 2: // 分类管理
                Category.LoadData();
                break;
            case 3: // 设置
                Settings.LoadData();
                break;
        }
    }

    [RelayCommand]
    private void SetToday()
    {
        _isLoading = true;
        StartDate = DateTime.Today;
        EndDate = DateTime.Today;
        SelectedPeriodName = "今天";
        _isLoading = false;
        RefreshDashboard();
    }

    [RelayCommand]
    private void SetThisWeek()
    {
        _isLoading = true;
        var today = DateTime.Today;
        // 周一为一周起点
        var diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        StartDate = today.AddDays(-diff);
        EndDate = today;
        SelectedPeriodName = "本周";
        _isLoading = false;
        RefreshDashboard();
    }

    [RelayCommand]
    private void SetThisMonth()
    {
        _isLoading = true;
        var today = DateTime.Today;
        StartDate = new DateTime(today.Year, today.Month, 1);
        EndDate = today;
        SelectedPeriodName = "本月";
        _isLoading = false;
        RefreshDashboard();
    }

    [RelayCommand]
    private void SetCustom()
    {
        SelectedPeriodName = "自定义";
        // 不自动修改日期，让用户自行选择
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        Dashboard.LoadData(StartDate, EndDate);
    }

    /// <summary>
    /// 释放资源：取消未完成的图标加载，清理子 ViewModel 缓存。
    /// 在窗口真正关闭时由 MainWindow 调用。
    /// </summary>
    public void Dispose()
    {
        AppList.Dispose();
        Dashboard.Dispose();
    }
}
