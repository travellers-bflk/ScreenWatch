using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ScreenWatch.App.Services;
using ScreenWatch.Core.Services;

namespace ScreenWatch.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private UsageTrackingService? _trackingService;
    private WpfTrayService? _trayService;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // 单实例检查：使用全局命名 Mutex 确保只有一个实例运行
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: @"Global\ScreenWatch_SingleInstance",
                out bool createdNew);

            if (!createdNew)
            {
                // 已有实例运行，提示后退出
                MessageBox.Show("ScreenWatch 已经在运行中。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 记录启动日志
            LogStartup("Application started");

            // 注册全局异常处理
            RegisterGlobalExceptionHandlers();

            // 初始化服务定位器（数据库、Repository、查询服务）
            ServiceHost.Initialize();
            LogStartup("ServiceHost initialized");

            // 创建并启动采集服务
            _trackingService = UsageTrackingService.CreateDefault();
            _trackingService.Start();
            LogStartup("Tracking service started");

            // 创建主窗口并显示
            // 注意：托盘图标由 App 通过 WpfTrayService 管理，不依赖第三方库
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.ShowInTaskbar = true;
            mainWindow.WindowState = WindowState.Normal;
            LogStartup("Main window created");

            // 显示主窗口用于调试
            mainWindow.Show();
            mainWindow.Activate();
            LogStartup("Main window shown");

            // 初始化系统托盘（使用 HwndSource + 原生 Shell_NotifyIcon，不依赖第三方库）
            _trayService = new WpfTrayService();
            _trayService.ShowWindowRequested += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is MainWindow mw)
                        mw.ShowWindowCommand();
                });
            };
            _trayService.ExitRequested += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is MainWindow mw)
                        mw.ForceClose();
                    Shutdown();
                });
            };
            _trayService.Initialize();
            LogStartup("Tray service initialized");
        }
        catch (Exception ex)
        {
            LogStartup($"CRITICAL ERROR: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"程序启动失败: {ex.Message}\n\n{ex.StackTrace}", "严重错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// 记录启动日志到文件（用于调试）
    /// </summary>
    private static void LogStartup(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ScreenWatch", "logs");
            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"startup_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
        }
        catch
        {
            // 忽略日志写入失败
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 停止采集服务并释放资源（含 IdleLockDetector 内部 Timer）
        _trackingService?.Dispose();
        _trayService?.Dispose();

        // 释放单实例 Mutex
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// 注册全局异常处理程序，捕获未处理的异常以防止应用静默崩溃。
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        // 捕获 UI 线程上的未处理异常
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 捕获非 UI 线程上的未处理异常
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // 捕获未观察到的 Task 异常
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 记录崩溃日志并提示用户，避免异常被静默吞掉导致应用无响应
        LogStartup($"Dispatcher unhandled exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
        MessageBox.Show($"发生未处理异常: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // 记录非 UI 线程的未处理异常（e.ExceptionObject 为 object 类型，需转换为 Exception）
        var ex = e.ExceptionObject as Exception;
        LogStartup($"AppDomain unhandled exception: {(ex != null ? ex.Message + "\n" + ex.StackTrace : e.ExceptionObject?.ToString() ?? "null")}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // 记录未观察到的 Task 异常，防止静默丢失错误信息
        LogStartup($"Unobserved task exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
        e.SetObserved();
    }
}


