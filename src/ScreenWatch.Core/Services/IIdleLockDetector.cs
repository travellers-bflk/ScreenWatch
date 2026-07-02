namespace ScreenWatch.Core.Services;

/// <summary>
/// Abstraction for idle-time and screen-lock detection, enabling unit tests
/// to control state and raise events without relying on Win32 / SystemEvents.
/// </summary>
public interface IIdleLockDetector
{
    /// <summary>True when the user has been idle longer than the configured threshold.</summary>
    bool IsIdle { get; }

    /// <summary>True when the session is locked.</summary>
    bool IsLocked { get; }

    /// <summary>Raised when idle time exceeds the threshold.</summary>
    event EventHandler IdleStarted;

    /// <summary>Raised when idle time drops back below the threshold.</summary>
    event EventHandler IdleEnded;

    /// <summary>Raised when the session is locked.</summary>
    event EventHandler Locked;

    /// <summary>Raised when the session is unlocked.</summary>
    event EventHandler Unlocked;

    /// <summary>Begins monitoring for idle and lock events.</summary>
    void Start();

    /// <summary>Stops monitoring and releases all event subscriptions.</summary>
    void Stop();
}
