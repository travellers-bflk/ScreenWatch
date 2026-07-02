using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;
using ScreenWatch.Core.Native;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Core coordinator for usage tracking. Manages foreground session lifecycle,
/// periodic background window enumeration, and idle/lock truncation.
/// A single application is never counted as both foreground and background simultaneously.
/// </summary>
public sealed class UsageTrackingService : IDisposable
{
    // ----- Dependencies -----
    private readonly IWindowService _windowService;
    private readonly IAppRepository _appRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IExclusionRepository _exclusionRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly AppResolver _appResolver;
    private readonly IIdleLockDetector _idleLockDetector;

    // ----- State (protected by _stateLock) -----
    private readonly object _stateLock = new();
    private ForegroundSession? _currentForegroundSession;
    private IntPtr _foregroundHwnd;
    private int _backgroundScanIntervalSeconds = 45;

    // ----- Resources -----
    private WinEventProc? _foregroundCallback; // Keep alive to prevent GC
    private IntPtr _hookHandle;
    private Timer? _backgroundTimer;
    private bool _running;

    /// <summary>
    /// True while the service is actively tracking.
    /// </summary>
    public bool IsRunning
    {
        get { lock (_stateLock) return _running; }
    }

    /// <summary>
    /// Full dependency-injection constructor for testability.
    /// </summary>
    public UsageTrackingService(
        IWindowService windowService,
        IAppRepository appRepository,
        ISessionRepository sessionRepository,
        IExclusionRepository exclusionRepository,
        ISettingsRepository settingsRepository,
        AppResolver appResolver,
        IIdleLockDetector idleLockDetector)
    {
        _windowService = windowService;
        _appRepository = appRepository;
        _sessionRepository = sessionRepository;
        _exclusionRepository = exclusionRepository;
        _settingsRepository = settingsRepository;
        _appResolver = appResolver;
        _idleLockDetector = idleLockDetector;
    }

    /// <summary>
    /// Factory method that wires up all production dependencies using DatabaseManager.Instance.
    /// </summary>
    public static UsageTrackingService CreateDefault()
    {
        var db = DatabaseManager.Instance;
        db.Initialize();

        var windowService = new WindowServiceAdapter();
        var appRepo = new AppRepository(db);
        var sessionRepo = new SessionRepository(db);
        var exclusionRepo = new ExclusionRepository(db);
        var settingsRepo = new SettingsRepository(db);
        var appResolver = new AppResolver(appRepo, windowService);

        var idleThreshold = settingsRepo.Get("idle_threshold_seconds", 300);
        var idleDetector = new IdleLockDetector(
            () => windowService.GetIdleSeconds(),
            idleThreshold);

        return new UsageTrackingService(
            windowService, appRepo, sessionRepo, exclusionRepo,
            settingsRepo, appResolver, idleDetector);
    }

    /// <summary>
    /// Starts tracking: reads settings, subscribes to events, hooks foreground changes,
    /// begins the first foreground session, and starts the background scan timer.
    /// </summary>
    public void Start()
    {
        lock (_stateLock)
        {
            if (_running)
                return;
            _running = true;
        }

        // Read configuration
        _backgroundScanIntervalSeconds = _settingsRepository.Get(
            "background_scan_interval_seconds", 45);

        // Apply window title capture setting (privacy control)
        WindowService.CaptureWindowTitle = _settingsRepository.Get("capture_window_title", false);

        // Subscribe to idle / lock events
        _idleLockDetector.IdleStarted += OnIdleStarted;
        _idleLockDetector.IdleEnded += OnIdleEnded;
        _idleLockDetector.Locked += OnLocked;
        _idleLockDetector.Unlocked += OnUnlocked;
        _idleLockDetector.Start();

        // Register foreground-change hook (keep delegate alive in field)
        _foregroundCallback = new WinEventProc(OnForegroundChanged);
        _hookHandle = _windowService.HookForegroundChanged(_foregroundCallback);

        // Begin tracking the current foreground window
        var foreground = _windowService.GetForegroundWindowInfo();
        if (foreground != null)
        {
            TryStartForegroundSession(foreground);
        }

        // Start background enumeration timer
        _backgroundTimer = new Timer(
            OnBackgroundScan,
            null,
            TimeSpan.FromSeconds(_backgroundScanIntervalSeconds),
            TimeSpan.FromSeconds(_backgroundScanIntervalSeconds));
    }

    /// <summary>
    /// Stops tracking: flushes the current foreground session, unhooks events,
    /// stops the background timer, and stops the idle/lock detector.
    /// </summary>
    public void Stop()
    {
        lock (_stateLock)
        {
            if (!_running)
                return;
            _running = false;
        }

        // End and persist the current foreground session
        EndForegroundSession(isIdle: false, isLocked: false);

        // Unhook foreground-change event
        if (_hookHandle != IntPtr.Zero)
        {
            _windowService.Unhook(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _foregroundCallback = null;

        // Stop background timer
        _backgroundTimer?.Dispose();
        _backgroundTimer = null;

        // Unsubscribe and stop idle/lock detector
        _idleLockDetector.IdleStarted -= OnIdleStarted;
        _idleLockDetector.IdleEnded -= OnIdleEnded;
        _idleLockDetector.Locked -= OnLocked;
        _idleLockDetector.Unlocked -= OnUnlocked;
        _idleLockDetector.Stop();
    }

    // ================================================================
    //  Foreground window change handling
    // ================================================================

    /// <summary>
    /// WinEvent callback invoked when the foreground window changes.
    /// Ends the previous session and starts a new one for the new foreground window.
    /// </summary>
    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only process actual window objects (not sub-objects)
        if (idObject != 0 || idChild != 0)
            return;

        // Skip if the same window is already foreground (spurious event)
        lock (_stateLock)
        {
            if (hwnd == _foregroundHwnd)
                return;
        }

        // End current session with current idle/lock flags
        var isIdle = _idleLockDetector.IsIdle;
        var isLocked = _idleLockDetector.IsLocked;
        EndForegroundSession(isIdle, isLocked);

        // Get the new foreground window info
        var window = _windowService.GetForegroundWindowInfo();
        if (window == null)
        {
            lock (_stateLock)
            {
                _foregroundHwnd = IntPtr.Zero;
                _currentForegroundSession = null;
            }
            return;
        }

        TryStartForegroundSession(window);
    }

    // ================================================================
    //  Background window enumeration
    // ================================================================

    /// <summary>
    /// Timer callback: enumerates visible background windows (excluding the
    /// foreground hwnd and the foreground app), and batch-inserts background sessions.
    /// Skipped entirely when the user is idle or the session is locked.
    /// </summary>
    private void OnBackgroundScan(object? state)
    {
        // Guard against race with Stop(): if the service is no longer running,
        // do not enumerate windows or insert sessions.
        if (!IsRunning)
            return;

        // Skip background scanning when the user is away
        if (_idleLockDetector.IsIdle || _idleLockDetector.IsLocked)
            return;

        IntPtr foregroundHwnd;
        int? foregroundAppId;
        lock (_stateLock)
        {
            foregroundHwnd = _foregroundHwnd;
            foregroundAppId = _currentForegroundSession?.AppId;
        }

        // Enumerate visible windows, explicitly excluding the foreground window
        var windows = _windowService.EnumerateVisibleTopLevelWindows(excludeHwnd: foregroundHwnd);
        if (windows.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var interval = _backgroundScanIntervalSeconds;
        var startTime = now.AddSeconds(-interval);

        var sessions = new List<UsageSession>();
        var seenAppIds = new HashSet<int>();

        foreach (var window in windows)
        {
            var app = _appResolver.ResolveApp(window);
            if (app == null)
                continue;

            // Check exclusion whitelist
            if (_exclusionRepository.IsExcluded(app.ExePath, app.ExeName))
                continue;

            // Anti-duplicate: skip if this is the same app as the current foreground
            if (foregroundAppId.HasValue && app.AppId == foregroundAppId.Value)
                continue;

            // Only one background session per app per scan cycle
            if (!seenAppIds.Add(app.AppId))
                continue;

            sessions.Add(new UsageSession
            {
                AppId = app.AppId,
                SessionType = "background",
                StartTime = startTime,
                EndTime = now,
                DurationSec = interval,
                IsIdle = false,
                IsLocked = false
            });
        }

        if (sessions.Count > 0)
            _sessionRepository.InsertSessions(sessions);
    }

    // ================================================================
    //  Idle / Lock event handlers
    // ================================================================

    /// <summary>
    /// Idle started: immediately end the current foreground session (marked is_idle)
    /// and null out the session so no further time accrues until idle ends.
    /// </summary>
    private void OnIdleStarted(object? sender, EventArgs e)
    {
        EndForegroundSession(isIdle: true, isLocked: _idleLockDetector.IsLocked);
    }

    /// <summary>
    /// Idle ended: re-acquire the foreground window and start a fresh session.
    /// </summary>
    private void OnIdleEnded(object? sender, EventArgs e)
    {
        var window = _windowService.GetForegroundWindowInfo();
        if (window != null)
            TryStartForegroundSession(window);
    }

    /// <summary>
    /// Session locked: end the current foreground session (marked is_locked)
    /// and null out the session.
    /// </summary>
    private void OnLocked(object? sender, EventArgs e)
    {
        EndForegroundSession(isIdle: _idleLockDetector.IsIdle, isLocked: true);
    }

    /// <summary>
    /// Session unlocked: re-acquire the foreground window and start a fresh session.
    /// </summary>
    private void OnUnlocked(object? sender, EventArgs e)
    {
        var window = _windowService.GetForegroundWindowInfo();
        if (window != null)
            TryStartForegroundSession(window);
    }

    // ================================================================
    //  Session lifecycle helpers
    // ================================================================

    /// <summary>
    /// Attempts to resolve the window's app, check the exclusion whitelist,
    /// and begin a new foreground session. If the app is excluded or resolution
    /// fails, no session is started.
    /// </summary>
    private void TryStartForegroundSession(WindowInfo window)
    {
        var app = _appResolver.ResolveApp(window);
        if (app == null)
        {
            lock (_stateLock)
            {
                _foregroundHwnd = window.Hwnd;
                _currentForegroundSession = null;
            }
            return;
        }

        // Check exclusion whitelist — excluded apps do not create sessions
        if (_exclusionRepository.IsExcluded(app.ExePath, app.ExeName))
        {
            lock (_stateLock)
            {
                _foregroundHwnd = window.Hwnd;
                _currentForegroundSession = null;
            }
            return;
        }

        lock (_stateLock)
        {
            _foregroundHwnd = window.Hwnd;
            _currentForegroundSession = new ForegroundSession
            {
                AppId = app.AppId,
                StartTime = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Ends the current foreground session (if any), computes duration,
    /// and persists it with the given idle/lock flags. Thread-safe.
    /// </summary>
    private void EndForegroundSession(bool isIdle, bool isLocked)
    {
        ForegroundSession? session;
        lock (_stateLock)
        {
            session = _currentForegroundSession;
            _currentForegroundSession = null;
        }

        if (session == null)
            return;

        var now = DateTime.UtcNow;
        var duration = Math.Max(1, (int)(now - session.StartTime).TotalSeconds);

        try
        {
            _sessionRepository.InsertSession(new UsageSession
            {
                AppId = session.AppId,
                SessionType = "foreground",
                StartTime = session.StartTime,
                EndTime = now,
                DurationSec = duration,
                IsIdle = isIdle,
                IsLocked = isLocked
            });
        }
        catch
        {
            // Database write failure — session is lost but service continues
        }
    }

    // ================================================================
    //  Test hooks (internal, for unit test access only)
    // ================================================================

    /// <summary>
    /// Triggers a background scan immediately. For unit testing only.
    /// </summary>
    internal void TriggerBackgroundScan() => OnBackgroundScan(null);

    /// <summary>
    /// Simulates a foreground-window-changed event for unit testing.
    /// </summary>
    internal void SimulateForegroundChanged(IntPtr hwnd)
        => OnForegroundChanged(IntPtr.Zero, 0, hwnd, 0, 0, 0, 0);

    /// <summary>
    /// Gets the current foreground AppId (for test assertions). Returns null if no active session.
    /// </summary>
    internal int? CurrentForegroundAppId
    {
        get { lock (_stateLock) return _currentForegroundSession?.AppId; }
    }

    /// <summary>
    /// Disposes the service: stops tracking and disposes the idle/lock detector's
    /// internal timer. Safe to call multiple times (Stop has a _running guard).
    /// </summary>
    public void Dispose()
    {
        Stop();
        (_idleLockDetector as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Immutable snapshot of a foreground session's essential state.
    /// </summary>
    private sealed class ForegroundSession
    {
        public int AppId { get; init; }
        public DateTime StartTime { get; init; }
    }
}
