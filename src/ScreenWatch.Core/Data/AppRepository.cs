using Microsoft.Data.Sqlite;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public class AppRepository : IAppRepository
{
    private readonly DatabaseManager _db;

    public AppRepository(DatabaseManager db)
    {
        _db = db;
    }

    public AppInfo? GetOrCreateApp(string exePath, string exeName, string? displayName = null)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var transaction = conn.BeginTransaction();

            // Try to find existing app by exe_path
            using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = "SELECT * FROM apps WHERE exe_path = @exePath";
                selectCmd.Parameters.AddWithValue("@exePath", exePath);
                using var reader = selectCmd.ExecuteReader();
                if (reader.Read())
                {
                    var app = MapApp(reader);
                    transaction.Commit();
                    return app;
                }
            }

            // Insert new app
            int newAppId;
            var now = DateTime.UtcNow;
            using (var insertCmd = conn.CreateCommand())
            {
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = """
                    INSERT INTO apps (exe_path, exe_name, display_name, first_seen, is_recognized)
                    VALUES (@exePath, @exeName, @displayName, @firstSeen, 0);
                    """;
                insertCmd.Parameters.AddWithValue("@exePath", exePath);
                insertCmd.Parameters.AddWithValue("@exeName", exeName);
                insertCmd.Parameters.AddWithValue("@displayName", (object?)displayName ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@firstSeen", now.ToString("o"));
                insertCmd.ExecuteNonQuery();
            }

            using (var idCmd = conn.CreateCommand())
            {
                idCmd.Transaction = transaction;
                idCmd.CommandText = "SELECT last_insert_rowid();";
                newAppId = (int)(long)idCmd.ExecuteScalar()!;
            }

            transaction.Commit();

            return new AppInfo
            {
                AppId = newAppId,
                ExePath = exePath,
                ExeName = exeName,
                DisplayName = displayName ?? string.Empty,
                CategoryId = null,
                IconCacheKey = null,
                FirstSeen = now,
                IsRecognized = false
            };
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public AppInfo? GetById(int appId)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM apps WHERE app_id = @appId";
        cmd.Parameters.AddWithValue("@appId", appId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapApp(reader) : null;
    }

    public void UpdateCategory(int appId, int categoryId)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE apps SET category_id = @categoryId, is_recognized = 1 WHERE app_id = @appId";
            cmd.Parameters.AddWithValue("@categoryId", categoryId);
            cmd.Parameters.AddWithValue("@appId", appId);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void UpdateDisplayName(int appId, string displayName)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE apps SET display_name = @displayName WHERE app_id = @appId";
            cmd.Parameters.AddWithValue("@displayName", displayName);
            cmd.Parameters.AddWithValue("@appId", appId);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public List<AppInfo> GetUnrecognizedApps()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM apps WHERE is_recognized = 0 ORDER BY first_seen DESC";
        using var reader = cmd.ExecuteReader();
        var apps = new List<AppInfo>();
        while (reader.Read())
        {
            apps.Add(MapApp(reader));
        }
        return apps;
    }

    public List<AppInfo> GetAllApps()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM apps ORDER BY display_name";
        using var reader = cmd.ExecuteReader();
        var apps = new List<AppInfo>();
        while (reader.Read())
        {
            apps.Add(MapApp(reader));
        }
        return apps;
    }

    /// <inheritdoc/>
    public void ClearAllCategories()
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE apps SET category_id = NULL, is_recognized = 0";
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    private static AppInfo MapApp(SqliteDataReader reader)
    {
        return new AppInfo
        {
            AppId = reader.GetInt("app_id"),
            ExePath = reader.GetString("exe_path"),
            ExeName = reader.GetString("exe_name"),
            DisplayName = reader.GetNullableString("display_name") ?? string.Empty,
            CategoryId = reader.GetNullableInt("category_id"),
            IconCacheKey = reader.GetNullableString("icon_cache_key"),
            FirstSeen = reader.GetDateTime("first_seen"),
            IsRecognized = reader.GetBool("is_recognized")
        };
    }
}
