using ScreenWatch.Core.Data;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Tests.Data;

public class SessionRepositoryTests : IDisposable
{
    private readonly TestDatabase _testDb;
    private readonly SessionRepository _sessionRepo;
    private readonly AppRepository _appRepo;

    public SessionRepositoryTests()
    {
        _testDb = new TestDatabase();
        _sessionRepo = new SessionRepository(_testDb.Db);
        _appRepo = new AppRepository(_testDb.Db);
    }

    public void Dispose() => _testDb.Dispose();

    private AppInfo CreateTestApp()
    {
        return _appRepo.GetOrCreateApp(@"C:\test\app.exe", "app.exe")!;
    }

    [Fact]
    public void InsertSession_InsertsOneRecord()
    {
        var app = CreateTestApp();
        var now = DateTime.UtcNow;

        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = app.AppId,
            SessionType = "foreground",
            StartTime = now.AddMinutes(-5),
            EndTime = now,
            DurationSec = 300,
            IsIdle = false,
            IsLocked = false
        });

        var sessions = _sessionRepo.GetSessionsByRange(now.AddMinutes(-10), now.AddMinutes(5));
        Assert.Single(sessions);
        Assert.Equal("foreground", sessions[0].SessionType);
        Assert.Equal(300, sessions[0].DurationSec);
        Assert.False(sessions[0].IsIdle);
    }

    [Fact]
    public void GetSessionsByRange_ReturnsOnlySessionsInRange()
    {
        var app = CreateTestApp();
        var baseTime = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = app.AppId,
            SessionType = "foreground",
            StartTime = baseTime.AddHours(-2),
            EndTime = baseTime.AddHours(-2).AddMinutes(5),
            DurationSec = 300
        });

        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = app.AppId,
            SessionType = "foreground",
            StartTime = baseTime,
            EndTime = baseTime.AddMinutes(10),
            DurationSec = 600
        });

        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = app.AppId,
            SessionType = "foreground",
            StartTime = baseTime.AddHours(3),
            EndTime = baseTime.AddHours(3).AddMinutes(5),
            DurationSec = 300
        });

        var sessions = _sessionRepo.GetSessionsByRange(baseTime.AddMinutes(-5), baseTime.AddMinutes(15));
        Assert.Single(sessions);
        Assert.Equal(600, sessions[0].DurationSec);
    }

    [Fact]
    public void GetSessionsByRange_WithSessionTypeFilter()
    {
        var app = CreateTestApp();
        var now = DateTime.UtcNow;

        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = app.AppId,
            SessionType = "foreground",
            StartTime = now.AddMinutes(-5),
            EndTime = now,
            DurationSec = 300
        });

        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = app.AppId,
            SessionType = "background",
            StartTime = now.AddMinutes(-5),
            EndTime = now,
            DurationSec = 300
        });

        var foregroundOnly = _sessionRepo.GetSessionsByRange(now.AddMinutes(-10), now.AddMinutes(5), "foreground");
        Assert.Single(foregroundOnly);
        Assert.Equal("foreground", foregroundOnly[0].SessionType);
    }

    [Fact]
    public void GetSessionsByRange_NoResults_ReturnsEmptyList()
    {
        var sessions = _sessionRepo.GetSessionsByRange(DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        Assert.Empty(sessions);
    }

    [Fact]
    public void InsertSessions_BatchInsertsAllInTransaction()
    {
        var app = CreateTestApp();
        var now = DateTime.UtcNow;

        var sessions = new List<UsageSession>
        {
            new() { AppId = app.AppId, SessionType = "foreground", StartTime = now.AddMinutes(-30), EndTime = now.AddMinutes(-25), DurationSec = 300, IsIdle = false, IsLocked = false },
            new() { AppId = app.AppId, SessionType = "foreground", StartTime = now.AddMinutes(-20), EndTime = now.AddMinutes(-15), DurationSec = 300, IsIdle = true, IsLocked = false },
            new() { AppId = app.AppId, SessionType = "background", StartTime = now.AddMinutes(-10), EndTime = now.AddMinutes(-5), DurationSec = 300, IsIdle = false, IsLocked = true }
        };

        _sessionRepo.InsertSessions(sessions);

        var result = _sessionRepo.GetSessionsByRange(now.AddMinutes(-35), now.AddMinutes(5));
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void InsertSessions_EmptyCollection_DoesNotThrow()
    {
        _sessionRepo.InsertSessions([]);
        // No exception expected
    }

    [Fact]
    public void InsertSession_StoresBoolFlagsCorrectly()
    {
        var app = CreateTestApp();
        var now = DateTime.UtcNow;

        _sessionRepo.InsertSession(new UsageSession
        {
            AppId = app.AppId,
            SessionType = "foreground",
            StartTime = now.AddMinutes(-1),
            EndTime = now,
            DurationSec = 60,
            IsIdle = true,
            IsLocked = true
        });

        var sessions = _sessionRepo.GetSessionsByRange(now.AddMinutes(-5), now.AddMinutes(5));
        Assert.Single(sessions);
        Assert.True(sessions[0].IsIdle);
        Assert.True(sessions[0].IsLocked);
    }
}
