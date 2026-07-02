using Microsoft.Data.Sqlite;

namespace ScreenWatch.Core.Data;

/// <summary>
/// Executes database migrations in version order, tracking state via PRAGMA user_version
/// and a schema_history table.
/// </summary>
public class MigrationRunner
{
    private readonly DatabaseManager _db;

    /// <summary>
    /// The ordered list of all known migrations.
    /// </summary>
    public static readonly IReadOnlyList<Migration> Migrations = new[]
    {
        new Migration(1, "001_initial_schema", MigrationV1.Sql),
        new Migration(2, "002_category_icons_and_expanded_categories", MigrationV2.Sql),
    };

    public MigrationRunner(DatabaseManager db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the current database version as reported by PRAGMA user_version.
    /// </summary>
    public int GetCurrentVersion()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    /// <summary>
    /// Returns the list of migrations that have not yet been applied.
    /// </summary>
    public List<Migration> GetPendingMigrations()
    {
        var current = GetCurrentVersion();
        return Migrations.Where(m => m.Version > current).ToList();
    }

    /// <summary>
    /// Runs all pending migrations. Each migration runs in its own transaction.
    /// The database is backed up before any migration is applied (file databases only).
    /// </summary>
    public void RunMigrations()
    {
        var pending = GetPendingMigrations();
        if (pending.Count == 0)
        {
            return;
        }

        // Backup before applying migrations (only for file-based databases)
        _db.BackupDatabase();

        EnsureSchemaHistoryTable();

        foreach (var migration in pending)
        {
            ApplyMigration(migration);
        }
    }

    private void EnsureSchemaHistoryTable()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_history (
                version INTEGER PRIMARY KEY,
                script_name TEXT NOT NULL,
                executed_at TEXT NOT NULL,
                success INTEGER NOT NULL,
                duration_ms INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private void ApplyMigration(Migration migration)
    {
        using var conn = _db.CreateConnection();
        using var transaction = conn.BeginTransaction();

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Execute the migration SQL
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = migration.Sql;
                cmd.ExecuteNonQuery();
            }

            // Update PRAGMA user_version
            using (var versionCmd = conn.CreateCommand())
            {
                versionCmd.Transaction = transaction;
                versionCmd.CommandText = $"PRAGMA user_version = {migration.Version};";
                versionCmd.ExecuteNonQuery();
            }

            // Record in schema_history
            using (var histCmd = conn.CreateCommand())
            {
                histCmd.Transaction = transaction;
                histCmd.CommandText = """
                    INSERT INTO schema_history (version, script_name, executed_at, success, duration_ms)
                    VALUES (@version, @scriptName, @executedAt, @success, @durationMs);
                    """;
                histCmd.Parameters.AddWithValue("@version", migration.Version);
                histCmd.Parameters.AddWithValue("@scriptName", migration.ScriptName);
                histCmd.Parameters.AddWithValue("@executedAt", DateTime.UtcNow.ToString("o"));
                histCmd.Parameters.AddWithValue("@success", 1);
                histCmd.Parameters.AddWithValue("@durationMs", sw.ElapsedMilliseconds);
                histCmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            // Record the failed migration attempt
            RecordFailedMigration(conn, migration);
            throw;
        }
    }

    private void RecordFailedMigration(SqliteConnection conn, Migration migration)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO schema_history (version, script_name, executed_at, success, duration_ms)
                VALUES (@version, @scriptName, @executedAt, 0, 0);
                """;
            cmd.Parameters.AddWithValue("@version", migration.Version);
            cmd.Parameters.AddWithValue("@scriptName", migration.ScriptName);
            cmd.Parameters.AddWithValue("@executedAt", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // If recording the failure also fails, swallow the error to preserve the original exception
        }
    }
}
