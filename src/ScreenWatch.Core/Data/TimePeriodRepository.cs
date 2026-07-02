using Microsoft.Data.Sqlite;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public class TimePeriodRepository : ITimePeriodRepository
{
    private readonly DatabaseManager _db;

    public TimePeriodRepository(DatabaseManager db)
    {
        _db = db;
    }

    public List<TimePeriod> GetAll()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM time_periods ORDER BY period_id";
        using var reader = cmd.ExecuteReader();
        var periods = new List<TimePeriod>();
        while (reader.Read())
        {
            periods.Add(MapTimePeriod(reader));
        }
        return periods;
    }

    public int Add(TimePeriod period)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO time_periods (name, start_time, end_time, weekdays, enabled)
                VALUES (@name, @startTime, @endTime, @weekdays, @enabled);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@name", period.Name);
            cmd.Parameters.AddWithValue("@startTime", period.StartTime.ToString("HH:mm"));
            cmd.Parameters.AddWithValue("@endTime", period.EndTime.ToString("HH:mm"));
            cmd.Parameters.AddWithValue("@weekdays", (object?)period.Weekdays ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@enabled", period.Enabled ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar()!;
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void Update(TimePeriod period)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE time_periods
                SET name = @name, start_time = @startTime, end_time = @endTime,
                    weekdays = @weekdays, enabled = @enabled
                WHERE period_id = @periodId
                """;
            cmd.Parameters.AddWithValue("@name", period.Name);
            cmd.Parameters.AddWithValue("@startTime", period.StartTime.ToString("HH:mm"));
            cmd.Parameters.AddWithValue("@endTime", period.EndTime.ToString("HH:mm"));
            cmd.Parameters.AddWithValue("@weekdays", (object?)period.Weekdays ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@enabled", period.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@periodId", period.PeriodId);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void Delete(int id)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM time_periods WHERE period_id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    private static TimePeriod MapTimePeriod(SqliteDataReader reader)
    {
        return new TimePeriod
        {
            PeriodId = reader.GetInt("period_id"),
            Name = reader.GetString("name"),
            StartTime = reader.GetTimeOnly("start_time"),
            EndTime = reader.GetTimeOnly("end_time"),
            Weekdays = reader.GetNullableString("weekdays") ?? string.Empty,
            Enabled = reader.GetBool("enabled")
        };
    }
}
