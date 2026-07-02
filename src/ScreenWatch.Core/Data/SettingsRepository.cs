using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ScreenWatch.Core.Data;

public class SettingsRepository : ISettingsRepository
{
    private readonly DatabaseManager _db;

    public SettingsRepository(DatabaseManager db)
    {
        _db = db;
    }

    public string? Get(string key)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result is string s ? s : null;
    }

    public void Set(string key, string value)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO settings (key, value) VALUES (@key, @value)
                ON CONFLICT(key) DO UPDATE SET value = @value
                """;
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public T Get<T>(string key, T defaultValue)
    {
        var value = Get(key);
        if (value == null)
        {
            return defaultValue;
        }

        var type = typeof(T);

        try
        {
            if (type == typeof(string))
                return (T)(object)value;
            if (type == typeof(int))
                return (T)(object)int.Parse(value);
            if (type == typeof(long))
                return (T)(object)long.Parse(value);
            if (type == typeof(bool))
                return (T)(object)bool.Parse(value);
            if (type == typeof(double))
                return (T)(object)double.Parse(value);
            if (type == typeof(float))
                return (T)(object)float.Parse(value);
            if (type == typeof(DateTime))
                return (T)(object)DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
            if (type.IsEnum)
                return (T)Enum.Parse(type, value, ignoreCase: true);

            return JsonSerializer.Deserialize<T>(value) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
