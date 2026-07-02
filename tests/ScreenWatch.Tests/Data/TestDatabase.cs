using Microsoft.Data.Sqlite;
using ScreenWatch.Core.Data;

namespace ScreenWatch.Tests.Data;

/// <summary>
/// Creates a temporary file-based SQLite database for test isolation.
/// Each instance gets its own unique temp file, initialized with migrations.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public DatabaseManager Db { get; }
    private readonly string _tempFile;
    private bool _disposed;

    public TestDatabase()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"sw_test_{Guid.NewGuid():N}.db");
        Db = new DatabaseManager(_tempFile);
        Db.Initialize();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SqliteConnection.ClearAllPools();

        TryDeleteFile(_tempFile);
        TryDeleteFile(_tempFile + ".bak");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; temp files are in the system temp directory.
        }
    }
}
