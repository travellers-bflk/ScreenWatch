using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenWatch.App.ViewModels;

namespace ScreenWatch.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _forceClose;
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // 创建并绑定主视图模型
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // 窗口加载后初始化数据与托盘图标
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();

        // 默认选中"使用汇总"
        if (NavigationList != null && NavigationList.SelectedIndex < 0)
            NavigationList.SelectedIndex = 0;

    }

    /// <summary>
    /// 显示窗口（供托盘双击调用）
    /// </summary>
    public void ShowWindowCommand()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    /// <summary>
    /// 托盘菜单“退出”点击事件
    /// </summary>
    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ForceClose();
    }

    /// <summary>
    /// 重写关闭行为：普通关闭转为隐藏到托盘，仅 ForceClose() 时真正关闭。
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose)
        {
            // 阻止真正关闭，改为隐藏到托盘
            e.Cancel = true;
            ShowInTaskbar = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// 窗口真正关闭后释放 ViewModel 资源（取消图标加载、清理缓存）。
    /// 仅在 ForceClose 路径触发，隐藏到托盘时不执行。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// 强制关闭窗口，绕过 OnClosing 的隐藏拦截。供托盘"退出"时调用。
    /// </summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    /// <summary>
    /// macOS 红绿灯按钮：关闭/最小化/最大化。
    /// 通过 Tag 区分按钮身份。
    /// </summary>
    private void OnTrafficLightClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
            return;

        switch (tag)
        {
            case "Close":
                // 关闭窗口：由 OnClosing 拦截为隐藏到托盘，与托盘行为保持一致
                Close();
                break;
            case "Minimize":
                WindowState = WindowState.Minimized;
                break;
            case "Maximize":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
        }
    }

    /// <summary>
    /// 侧边栏导航切换：将选中项索引同步到视图模型的 SelectedTabIndex，
    /// 由 TabControl 绑定驱动内容区视图切换并触发懒加载。
    /// </summary>
    private void OnNavigationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || NavigationList == null)
            return;

        _viewModel.SelectedTabIndex = NavigationList.SelectedIndex;
    }

    /// <summary>
    /// 拖动无边框窗口；双击标题栏切换最大化/还原。
    /// </summary>
    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (WindowState == WindowState.Maximized)
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove 在非鼠标按下时机调用会抛异常，安全忽略
        }
    }

    /// <summary>
    /// 最大化时去掉窗口圆角，还原时恢复，避免透明圆角露出桌面。
    /// </summary>
    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        if (WindowRoot == null)
            return;

        WindowRoot.CornerRadius = WindowState == WindowState.Maximized
            ? new CornerRadius(0)
            : new CornerRadius(10);
    }
}
