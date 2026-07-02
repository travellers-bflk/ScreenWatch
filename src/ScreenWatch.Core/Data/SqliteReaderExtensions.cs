using Microsoft.Data.Sqlite;

namespace ScreenWatch.Core.Data;

/// <summary>
/// Extension methods for safely reading values from <see cref="SqliteDataReader"/>.
/// </summary>
internal static class SqliteReaderExtensions
{
    public static int GetInt(this SqliteDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name));
    }

    public static long GetLong(this SqliteDataReader reader, string name)
    {
        return reader.GetInt64(reader.GetOrdinal(name));
    }

    public static string GetString(this SqliteDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    public static string? GetNullableString(this SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int? GetNullableInt(this SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static bool GetBool(this SqliteDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name)) != 0;
    }

    public static DateTime GetDateTime(this SqliteDataReader reader, string name)
    {
        return DateTime.Parse(reader.GetString(reader.GetOrdinal(name)), null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static TimeOnly GetTimeOnly(this SqliteDataReader reader, string name)
    {
        return TimeOnly.Parse(reader.GetString(reader.GetOrdinal(name)));
    }
}
