using Microsoft.Data.Sqlite;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public class SessionRepository : ISessionRepository
{
    private readonly DatabaseManager _db;

    public SessionRepository(DatabaseManager db)
    {
        _db = db;
    }

    public void InsertSession(UsageSession session)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO usage_sessions
                    (app_id, session_type, start_time, end_time, duration_sec, is_idle, is_locked)
                VALUES
                    (@appId, @sessionType, @startTime, @endTime, @durationSec, @isIdle, @isLocked);
                """;
            AddSessionParameters(cmd, session);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void InsertSessions(IEnumerable<UsageSession> sessions)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var transaction = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO usage_sessions
                    (app_id, session_type, start_time, end_time, duration_sec, is_idle, is_locked)
                VALUES
                    (@appId, @sessionType, @startTime, @endTime, @durationSec, @isIdle, @isLocked);
                """;

            cmd.Parameters.Add("@appId", SqliteType.Integer);
            cmd.Parameters.Add("@sessionType", SqliteType.Text);
            cmd.Parameters.Add("@startTime", SqliteType.Text);
            cmd.Parameters.Add("@endTime", SqliteType.Text);
            cmd.Parameters.Add("@durationSec", SqliteType.Integer);
            cmd.Parameters.Add("@isIdle", SqliteType.Integer);
            cmd.Parameters.Add("@isLocked", SqliteType.Integer);

            foreach (var session in sessions)
            {
                cmd.Parameters["@appId"].Value = session.AppId;
                cmd.Parameters["@sessionType"].Value = session.SessionType;
                cmd.Parameters["@startTime"].Value = session.StartTime.ToString("o");
                cmd.Parameters["@endTime"].Value = session.EndTime.ToString("o");
                cmd.Parameters["@durationSec"].Value = session.DurationSec;
                cmd.Parameters["@isIdle"].Value = session.IsIdle ? 1 : 0;
                cmd.Parameters["@isLocked"].Value = session.IsLocked ? 1 : 0;
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public List<UsageSession> GetSessionsByRange(DateTime start, DateTime end, string? sessionType = null)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();

        var startStr = start.ToString("o");
        var endStr = end.ToString("o");

        if (string.IsNullOrEmpty(sessionType))
        {
            cmd.CommandText = """
                SELECT * FROM usage_sessions
                WHERE start_time >= @startStr AND start_time <= @endStr
                ORDER BY start_time
                """;
            cmd.Parameters.AddWithValue("@startStr", startStr);
            cmd.Parameters.AddWithValue("@endStr", endStr);
        }
        else
        {
            cmd.CommandText = """
                SELECT * FROM usage_sessions
                WHERE start_time >= @startStr AND start_time <= @endStr
                  AND session_type = @sessionType
                ORDER BY start_time
                """;
            cmd.Parameters.AddWithValue("@startStr", startStr);
            cmd.Parameters.AddWithValue("@endStr", endStr);
            cmd.Parameters.AddWithValue("@sessionType", sessionType);
        }

        using var reader = cmd.ExecuteReader();
        var sessions = new List<UsageSession>();
        while (reader.Read())
        {
            sessions.Add(MapSession(reader));
        }
        return sessions;
    }

    private static void AddSessionParameters(SqliteCommand cmd, UsageSession session)
    {
        cmd.Parameters.AddWithValue("@appId", session.AppId);
        cmd.Parameters.AddWithValue("@sessionType", session.SessionType);
        cmd.Parameters.AddWithValue("@startTime", session.StartTime.ToString("o"));
        cmd.Parameters.AddWithValue("@endTime", session.EndTime.ToString("o"));
        cmd.Parameters.AddWithValue("@durationSec", session.DurationSec);
        cmd.Parameters.AddWithValue("@isIdle", session.IsIdle ? 1 : 0);
        cmd.Parameters.AddWithValue("@isLocked", session.IsLocked ? 1 : 0);
    }

    private static UsageSession MapSession(SqliteDataReader reader)
    {
        return new UsageSession
        {
            SessionId = reader.GetInt("session_id"),
            AppId = reader.GetInt("app_id"),
            SessionType = reader.GetString("session_type"),
            StartTime = reader.GetDateTime("start_time"),
            EndTime = reader.GetDateTime("end_time"),
            DurationSec = reader.GetInt("duration_sec"),
            IsIdle = reader.GetBool("is_idle"),
            IsLocked = reader.GetBool("is_locked")
        };
    }
}
