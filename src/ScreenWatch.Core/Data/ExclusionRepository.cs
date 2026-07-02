using Microsoft.Data.Sqlite;
using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public class ExclusionRepository : IExclusionRepository
{
    private readonly DatabaseManager _db;

    public ExclusionRepository(DatabaseManager db)
    {
        _db = db;
    }

    public List<ExclusionRule> GetAll()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM exclusion_whitelist ORDER BY rule_id";
        using var reader = cmd.ExecuteReader();
        var rules = new List<ExclusionRule>();
        while (reader.Read())
        {
            rules.Add(MapRule(reader));
        }
        return rules;
    }

    public int Add(string matchType, string pattern)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO exclusion_whitelist (match_type, pattern, enabled)
                VALUES (@matchType, @pattern, 1);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@matchType", matchType);
            cmd.Parameters.AddWithValue("@pattern", pattern);
            return (int)(long)cmd.ExecuteScalar()!;
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void Remove(int ruleId)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM exclusion_whitelist WHERE rule_id = @ruleId";
            cmd.Parameters.AddWithValue("@ruleId", ruleId);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public void Toggle(int ruleId, bool enabled)
    {
        _db.WriteLock.Wait();
        try
        {
            using var conn = _db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE exclusion_whitelist SET enabled = @enabled WHERE rule_id = @ruleId";
            cmd.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@ruleId", ruleId);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _db.WriteLock.Release();
        }
    }

    public bool IsExcluded(string exePath, string exeName)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM exclusion_whitelist
            WHERE enabled = 1
              AND (
                (match_type = 'exe_path' AND @exePath LIKE pattern)
                OR
                (match_type = 'exe_name' AND @exeName LIKE pattern)
              )
            """;
        cmd.Parameters.AddWithValue("@exePath", exePath);
        cmd.Parameters.AddWithValue("@exeName", exeName);
        var result = cmd.ExecuteScalar();
        return result is long l && l > 0;
    }

    private static ExclusionRule MapRule(SqliteDataReader reader)
    {
        return new ExclusionRule
        {
            RuleId = reader.GetInt("rule_id"),
            MatchType = reader.GetString("match_type"),
            Pattern = reader.GetString("pattern"),
            Enabled = reader.GetBool("enabled")
        };
    }
}
