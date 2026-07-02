using Microsoft.Data.Sqlite;

namespace ScreenWatch.Core.Data;

/// <summary>
/// Manages the SQLite database connection and provides thread-safe access.
/// </summary>
public class DatabaseManager
{
    private static DatabaseManager? _defaultInstance;
    private static readonly object _instanceLock = new();

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Gets the full path to the database file, or ":memory:" for in-memory databases.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Gets the SQLite connection string.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Gets the semaphore used to serialize write operations.
    /// </summary>
    public SemaphoreSlim WriteLock => _writeLock;

    /// <summary>
    /// Gets the default singleton instance using the standard application data path.
    /// </summary>
    public static DatabaseManager Instance
    {
        get
        {
            if (_defaultInstance == null)
            {
                lock (_instanceLock)
                {
                    _defaultInstance ??= new DatabaseManager();
                }
            }

            return _defaultInstance;
        }
    }

    /// <summary>
    /// Creates a new DatabaseManager with the specified database path,
    /// or the default application data path if none is provided.
    /// </summary>
    /// <param name="databasePath">Custom database file path, or ":memory:" for in-memory. Pass null for default path.</param>
    public DatabaseManager(string? databasePath = null)
    {
        DatabasePath = databasePath ?? GetDefaultDatabasePath();
        ConnectionString = $"Data Source={DatabasePath}";

        if (DatabasePath != ":memory:")
        {
            var dir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }

    /// <summary>
    /// Creates and opens a new SQLite connection. The caller is responsible for disposing it.
    /// </summary>
    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Initializes the database by creating the file (if needed) and running all pending migrations.
    /// </summary>
    public void Initialize()
    {
        var runner = new MigrationRunner(this);
        runner.RunMigrations();
    }

    /// <summary>
    /// Backs up the database file to a .bak file. Skipped for in-memory databases or if the file does not exist.
    /// </summary>
    public void BackupDatabase()
    {
        if (DatabasePath == ":memory:" || !File.Exists(DatabasePath))
        {
            return;
        }

        var backupPath = DatabasePath + ".bak";
        File.Copy(DatabasePath, backupPath, overwrite: true);
    }

    /// <summary>
    /// Resets the static default instance. Primarily for testing.
    /// </summary>
    public static void ResetDefaultInstance()
    {
        lock (_instanceLock)
        {
            _defaultInstance = null;
        }
    }

    private static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ScreenWatch", "data", "usage.db");
    }
}
