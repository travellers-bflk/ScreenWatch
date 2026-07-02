using ScreenWatch.Core.Models;

namespace ScreenWatch.Core.Data;

public interface ISessionRepository
{
    /// <summary>
    /// Inserts a single usage session record.
    /// </summary>
    void InsertSession(UsageSession session);

    /// <summary>
    /// Batch-inserts multiple usage sessions within a single transaction.
    /// </summary>
    void InsertSessions(IEnumerable<UsageSession> sessions);

    /// <summary>
    /// Retrieves sessions within the given time range, optionally filtered by session type.
    /// </summary>
    List<UsageSession> GetSessionsByRange(DateTime start, DateTime end, string? sessionType = null);
}
