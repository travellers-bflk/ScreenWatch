using Microsoft.Data.Sqlite;
using ScreenWatch.Core.Data;

namespace ScreenWatch.Tests.Data;

public class DatabaseManagerTests : IDisposable
{
    private readonly TestDatabase _testDb;

    public DatabaseManagerTests()
    {
        _testDb = new TestDatabase();
    }

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public void Initialize_CreatesDatabaseFile()
    {
        Assert.True(File.Exists(_testDb.Db.DatabasePath));
    }

    [Fact]
    public void Initialize_CreatesAllExpectedTables()
    {
        var tableNames = GetTableNames();

        Assert.Contains("apps", tableNames);
        Assert.Contains("usage_sessions", tableNames);
        Assert.Contains("categories", tableNames);
        Assert.Contains("exclusion_whitelist", tableNames);
        Assert.Contains("time_periods", tableNames);
        Assert.Contains("settings", tableNames);
        Assert.Contains("schema_history", tableNames);
    }

    [Fact]
    public void Initialize_CreatesIndexes()
    {
        using var conn = _testDb.Db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'idx_sessions_%'";
        using var reader = cmd.ExecuteReader();

        var indexNames = new HashSet<string>();
        while (reader.Read())
        {
            indexNames.Add(reader.GetString(0));
        }

        Assert.Contains("idx_sessions_start", indexNames);
        Assert.Contains("idx_sessions_app", indexNames);
        Assert.Contains("idx_sessions_type", indexNames);
    }

    [Fact]
    public void Initialize_InsertsDefaultCategories()
    {
        using var conn = _testDb.Db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM categories";
        var count = (long)cmd.ExecuteScalar()!;
        // V1 seeds 5, V2 adds 4 more (开发, 工具, 媒体, 游戏)
        Assert.Equal(9, count);
    }

    [Fact]
    public void Initialize_SetsUserVersionToLatest()
    {
        var runner = new MigrationRunner(_testDb.Db);
        Assert.Equal(2, runner.GetCurrentVersion());
    }

    [Fact]
    public void Initialize_RecordsSchemaHistory()
    {
        using var conn = _testDb.Db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version, script_name, success FROM schema_history WHERE version = 1";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("001_initial_schema", reader.GetString(1));
        Assert.True(reader.GetInt32(2) != 0); // success = 1
    }

    [Fact]
    public void Initialize_IsIdempotent_CallingAgainDoesNotThrow()
    {
        // Act - should be a no-op
        _testDb.Db.Initialize();

        // Assert - version still 2 (V1 + V2 applied during construction)
        var runner = new MigrationRunner(_testDb.Db);
        Assert.Equal(2, runner.GetCurrentVersion());
    }

    [Fact]
    public void Initialize_IsIdempotent_DoesNotDuplicateCategories()
    {
        _testDb.Db.Initialize();

        using var conn = _testDb.Db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM categories";
        var count = (long)cmd.ExecuteScalar()!;
        Assert.Equal(9, count);
    }

    [Fact]
    public void Initialize_IsIdempotent_SchemaHistoryNotDuplicated()
    {
        _testDb.Db.Initialize();

        using var conn = _testDb.Db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM schema_history WHERE version = 1";
        var count = (long)cmd.ExecuteScalar()!;
        Assert.Equal(1, count);
    }

    [Fact]
    public void BackupDatabase_CreatesBackupFile()
    {
        // The database file should exist from initialization
        Assert.True(File.Exists(_testDb.Db.DatabasePath));

        _testDb.Db.BackupDatabase();

        Assert.True(File.Exists(_testDb.Db.DatabasePath + ".bak"));
    }

    private HashSet<string> GetTableNames()
    {
        using var conn = _testDb.Db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        using var reader = cmd.ExecuteReader();

        var names = new HashSet<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }
}
