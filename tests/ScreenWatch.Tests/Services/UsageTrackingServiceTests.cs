using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;
using ScreenWatch.Core.Native;
using ScreenWatch.Core.Services;
using ScreenWatch.Tests.Data;

namespace ScreenWatch.Tests.Services;

public class UsageTrackingServiceTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly AppRepository _appRepo;
    private readonly SessionRepository _sessionRepo;
    private readonly ExclusionRepository _exclusionRepo;
    private readonly SettingsRepository _settingsRepo;
    private readonly FakeWindowService _fakeWindow;
    private readonly TestIdleLockDetector _fakeDetector;
    private readonly AppResolver _resolver;
    private readonly UsageTrackingService _service;

    public UsageTrackingServiceTests()
    {
        _testDb = new TestDatabase();
        _appRepo = new AppRepository(_testDb.Db);
        _sessionRepo = new SessionRepository(_testDb.Db);
        _exclusionRepo = new ExclusionRepository(_testDb.Db);
        _settingsRepo = new SettingsRepository(_testDb.Db);
        _fakeWindow = new FakeWindowService();
        _fakeDetector = new TestIdleLockDetector();
        _resolver = new AppResolver(_appRepo, _fakeWindow, iconDir: Path.GetTempPath());

        // Set a large background scan interval to prevent the real timer from firing during tests
        _settingsRepo.Set("background_scan_interval_seconds", "3600");

        _service = new UsageTrackingService(
            _fakeWindow, _appRepo, _sessionRepo, _exclusionRepo,
            _settingsRepo, _resolver, _fakeDetector);
    }

    public void Dispose()
    {
        _service.Stop();
        _testDb.Dispose();
    }

    private static WindowInfo MakeWindow(IntPtr hwnd, string exePath, string processName) => new()
    {
        Hwnd = hwnd,
        ExePath = exePath,
        ProcessName = processName,
        Title = string.Empty,
        ClassName = string.Empty
    };

    private List<UsageSession> GetAllSessions() =>
        _sessionRepo.GetSessionsByRange(
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(2));

    // ====================================================================
    //  Exclusion whitelist tests
    // ====================================================================

    [Fact]
    public void Start_WithExcludedForegroundApp_DoesNotCreateSession()
    {
        // Arrange: add an exclusion rule for excluded.exe
        _exclusionRepo.Add("exe_name", "excluded.exe");
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\excluded.exe", "excluded.exe");

        // Act
        _service.Start();
        _service.Stop();

        // Assert: no sessions should have been created
        var sessions = GetAllSessions();
        Assert.Empty(sessions);
    }

    [Fact]
    public void BackgroundScan_WithExcludedApp_DoesNotCreateBackgroundSession()
    {
        // Arrange: foreground is app A, background includes an excluded app
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\fg.exe", "fg.exe");
        _fakeWindow.BackgroundWindows = new List<WindowInfo>
        {
            MakeWindow(new IntPtr(2), @"C:\test\excluded.exe", "excluded.exe"),
            MakeWindow(new IntPtr(3), @"C:\test\bg.exe", "bg.exe")
        };
        _exclusionRepo.Add("exe_name", "excluded.exe");

        // Act
        _service.Start();
        _service.TriggerBackgroundScan();
        _service.Stop();

        // Assert: only one background session for bg.exe, none for excluded.exe
        var sessions = GetAllSessions();
        var bgSessions = sessions.Where(s => s.SessionType == "background").ToList();
        Assert.Single(bgSessions);
        var bgApp = _appRepo.GetOrCreateApp(@"C:\test\bg.exe", "bg.exe");
        Assert.Equal(bgApp!.AppId, bgSessions[0].AppId);
    }

    // ====================================================================
    //  Idle truncation tests
    // ====================================================================

    [Fact]
    public void IdleStarted_EndsCurrentSessionWithIdleFlag()
    {
        // Arrange
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\app.exe", "app.exe");

        // Act
        _service.Start();
        Thread.Sleep(50); // let the session start time be slightly in the past
        _fakeDetector.RaiseIdleStarted();
        _service.Stop();

        // Assert: exactly one foreground session with is_idle = true
        var sessions = GetAllSessions();
        Assert.Single(sessions);
        Assert.Equal("foreground", sessions[0].SessionType);
        Assert.True(sessions[0].IsIdle);
        Assert.False(sessions[0].IsLocked);
    }

    [Fact]
    public void IdleEnded_StartsNewForegroundSession()
    {
        // Arrange
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\app.exe", "app.exe");

        // Act
        _service.Start();
        Thread.Sleep(50);
        _fakeDetector.RaiseIdleStarted();
        Thread.Sleep(50);
        _fakeDetector.RaiseIdleEnded();
        Thread.Sleep(50);
        _service.Stop();

        // Assert: two foreground sessions — first with is_idle, second without
        var sessions = GetAllSessions();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.Equal("foreground", s.SessionType));

        var ordered = sessions.OrderBy(s => s.StartTime).ToList();
        Assert.True(ordered[0].IsIdle);   // ended by idle
        Assert.False(ordered[1].IsIdle);  // started after idle ended, ended by Stop
    }

    [Fact]
    public void Locked_EndsCurrentSessionWithLockedFlag()
    {
        // Arrange
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\app.exe", "app.exe");

        // Act
        _service.Start();
        Thread.Sleep(50);
        _fakeDetector.RaiseLocked();
        _service.Stop();

        // Assert: one foreground session with is_locked = true
        var sessions = GetAllSessions();
        Assert.Single(sessions);
        Assert.Equal("foreground", sessions[0].SessionType);
        Assert.True(sessions[0].IsLocked);
    }

    [Fact]
    public void Unlocked_StartsNewForegroundSession()
    {
        // Arrange
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\app.exe", "app.exe");

        // Act
        _service.Start();
        Thread.Sleep(50);
        _fakeDetector.RaiseLocked();
        Thread.Sleep(50);
        _fakeDetector.RaiseUnlocked();
        Thread.Sleep(50);
        _service.Stop();

        // Assert: two foreground sessions — first locked, second not
        var sessions = GetAllSessions();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.Equal("foreground", s.SessionType));

        var ordered = sessions.OrderBy(s => s.StartTime).ToList();
        Assert.True(ordered[0].IsLocked);
        Assert.False(ordered[1].IsLocked);
    }

    // ====================================================================
    //  Anti-duplicate tests
    // ====================================================================

    [Fact]
    public void BackgroundScan_ExcludesForegroundApp()
    {
        // Arrange: foreground is app A (chrome), background has another chrome window + notepad
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\chrome.exe", "chrome.exe");
        _fakeWindow.BackgroundWindows = new List<WindowInfo>
        {
            // Same app as foreground (different hwnd, same exe path → same AppId)
            MakeWindow(new IntPtr(2), @"C:\test\chrome.exe", "chrome.exe"),
            // Different app
            MakeWindow(new IntPtr(3), @"C:\test\notepad.exe", "notepad.exe")
        };

        // Act
        _service.Start();
        _service.TriggerBackgroundScan();
        _service.Stop();

        // Assert
        var sessions = GetAllSessions();
        var chromeApp = _appRepo.GetOrCreateApp(@"C:\test\chrome.exe", "chrome.exe");
        var notepadApp = _appRepo.GetOrCreateApp(@"C:\test\notepad.exe", "notepad.exe");

        // Foreground session for chrome
        var fgSessions = sessions.Where(s => s.SessionType == "foreground").ToList();
        Assert.Single(fgSessions);
        Assert.Equal(chromeApp!.AppId, fgSessions[0].AppId);

        // Background session only for notepad, NOT for chrome
        var bgSessions = sessions.Where(s => s.SessionType == "background").ToList();
        Assert.Single(bgSessions);
        Assert.Equal(notepadApp!.AppId, bgSessions[0].AppId);
    }

    [Fact]
    public void BackgroundScan_ExcludesForegroundHwnd()
    {
        // Arrange: the foreground window's hwnd should be excluded from enumeration
        var fgHwnd = new IntPtr(100);
        _fakeWindow.ForegroundWindow = MakeWindow(fgHwnd, @"C:\test\fg.exe", "fg.exe");
        // Put a window with the same hwnd in background list — it should be filtered
        _fakeWindow.BackgroundWindows = new List<WindowInfo>
        {
            MakeWindow(fgHwnd, @"C:\test\fg.exe", "fg.exe"),
            MakeWindow(new IntPtr(200), @"C:\test\bg.exe", "bg.exe")
        };

        // Act
        _service.Start();
        _service.TriggerBackgroundScan();
        _service.Stop();

        // Assert: only one background session (bg.exe), the foreground hwnd was excluded
        var sessions = GetAllSessions();
        var bgSessions = sessions.Where(s => s.SessionType == "background").ToList();
        Assert.Single(bgSessions);

        var bgApp = _appRepo.GetOrCreateApp(@"C:\test\bg.exe", "bg.exe");
        Assert.Equal(bgApp!.AppId, bgSessions[0].AppId);
    }

    // ====================================================================
    //  Stop / cleanup tests
    // ====================================================================

    [Fact]
    public void Stop_FlushesCurrentForegroundSession()
    {
        // Arrange
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\app.exe", "app.exe");

        // Act
        _service.Start();
        Thread.Sleep(50);
        _service.Stop();

        // Assert: one foreground session was written
        var sessions = GetAllSessions();
        Assert.Single(sessions);
        Assert.Equal("foreground", sessions[0].SessionType);
        Assert.True(sessions[0].DurationSec >= 1);
        Assert.False(sessions[0].IsIdle);
        Assert.False(sessions[0].IsLocked);
    }

    [Fact]
    public void Start_BeginTracking_SetsIsRunning()
    {
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\app.exe", "app.exe");

        Assert.False(_service.IsRunning);
        _service.Start();
        Assert.True(_service.IsRunning);
        _service.Stop();
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public void BackgroundScan_SkippedWhenIdle()
    {
        // Arrange
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\fg.exe", "fg.exe");
        _fakeWindow.BackgroundWindows = new List<WindowInfo>
        {
            MakeWindow(new IntPtr(2), @"C:\test\bg.exe", "bg.exe")
        };

        // Act: start, then go idle, then trigger background scan
        _service.Start();
        _fakeDetector.IsIdle = true;
        _service.TriggerBackgroundScan();
        _service.Stop();

        // Assert: no background sessions (scan was skipped due to idle)
        var sessions = GetAllSessions();
        var bgSessions = sessions.Where(s => s.SessionType == "background").ToList();
        Assert.Empty(bgSessions);
    }

    [Fact]
    public void BackgroundScan_SkippedWhenLocked()
    {
        // Arrange
        _fakeWindow.ForegroundWindow = MakeWindow(new IntPtr(1), @"C:\test\fg.exe", "fg.exe");
        _fakeWindow.BackgroundWindows = new List<WindowInfo>
        {
            MakeWindow(new IntPtr(2), @"C:\test\bg.exe", "bg.exe")
        };

        // Act
        _service.Start();
        _fakeDetector.IsLocked = true;
        _service.TriggerBackgroundScan();
        _service.Stop();

        // Assert: no background sessions (scan was skipped due to lock)
        var sessions = GetAllSessions();
        var bgSessions = sessions.Where(s => s.SessionType == "background").ToList();
        Assert.Empty(bgSessions);
    }

    [Fact]
    public void ForegroundChanged_CreatesSeparateSessions()
    {
        // Arrange: initial foreground is app A
        var windowA = MakeWindow(new IntPtr(1), @"C:\test\appA.exe", "appA.exe");
        var windowB = MakeWindow(new IntPtr(2), @"C:\test\appB.exe", "appB.exe");
        _fakeWindow.ForegroundWindow = windowA;

        // Act
        _service.Start();
        Thread.Sleep(50);

        // Simulate foreground change to app B
        _fakeWindow.ForegroundWindow = windowB;
        _service.SimulateForegroundChanged(windowB.Hwnd);
        Thread.Sleep(50);

        _service.Stop();

        // Assert: two foreground sessions, one for each app
        var sessions = GetAllSessions();
        var fgSessions = sessions.Where(s => s.SessionType == "foreground")
            .OrderBy(s => s.StartTime).ToList();
        Assert.Equal(2, fgSessions.Count);

        var appA = _appRepo.GetOrCreateApp(@"C:\test\appA.exe", "appA.exe");
        var appB = _appRepo.GetOrCreateApp(@"C:\test\appB.exe", "appB.exe");
        Assert.Equal(appA!.AppId, fgSessions[0].AppId);
        Assert.Equal(appB!.AppId, fgSessions[1].AppId);
    }
}
