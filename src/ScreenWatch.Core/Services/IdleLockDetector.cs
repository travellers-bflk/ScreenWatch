using Microsoft.Win32;
using ScreenWatch.Core.Native;

namespace ScreenWatch.Core.Services;

/// <summary>
/// Detects user idle time and screen lock/unlock events.
/// Idle checking uses a periodic timer calling a delegate (default: <see cref="Native.WindowService.GetIdleSeconds"/>).
/// Lock detection subscribes to <see cref="SystemEvents.SessionSwitch"/>.
/// All events are raised on the same thread that detected the change.
/// </summary>
public sealed class IdleLockDetector : IIdleLockDetector
{
    private readonly Func<int> _getIdleSeconds;
    private readonly int _idleThresholdSeconds;
    private readonly Timer _idleTimer;
    private bool _isIdle;
    private bool _isLocked;
    private bool _started;
    private readonly object _stateLock = new();

    /// <inheritdoc />
    public bool IsIdle
    {
        get { lock (_stateLock) return _isIdle; }
    }

    /// <inheritdoc />
    public bool IsLocked
    {
        get { lock (_stateLock) return _isLocked; }
    }

    /// <inheritdoc />
    public event EventHandler? IdleStarted;

    /// <inheritdoc />
    public event EventHandler? IdleEnded;

    /// <inheritdoc />
    public event EventHandler? Locked;

    /// <inheritdoc />
    public event EventHandler? Unlocked;

    /// <summary>
    /// Creates an IdleLockDetector with the specified idle-check function and threshold.
    /// </summary>
    /// <param name="getIdleSeconds">Function returning the current idle seconds. Defaults to <see cref="Native.WindowService.GetIdleSeconds"/>.</param>
    /// <param name="idleThresholdSeconds">Seconds of idle time before triggering IdleStarted. Default 300.</param>
    public IdleLockDetector(Func<int>? getIdleSeconds = null, int idleThresholdSeconds = 300)
    {
        _getIdleSeconds = getIdleSeconds ?? WindowService.GetIdleSeconds;
        _idleThresholdSeconds = idleThresholdSeconds;
        _idleTimer = new Timer(CheckIdle, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public void Start()
    {
        lock (_stateLock)
        {
            if (_started)
                return;
            _started = true;
        }

        // Subscribe to system session switch events (lock / unlock)
        SystemEvents.SessionSwitch += OnSessionSwitch;

        // Start periodic idle check every 30 seconds
        _idleTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_stateLock)
        {
            if (!_started)
                return;
            _started = false;
        }

        _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    /// <summary>
    /// Periodic idle check callback. Compares current idle seconds against the threshold
    /// and raises IdleStarted / IdleEnded on state transitions.
    /// </summary>
    private void CheckIdle(object? state)
    {
        bool shouldRaiseStarted = false;
        bool shouldRaiseEnded = false;

        lock (_stateLock)
        {
            if (!_started)
                return;

            int idleSeconds;
            try
            {
                idleSeconds = _getIdleSeconds();
            }
            catch
            {
                idleSeconds = 0;
            }

            var wasIdle = _isIdle;
            _isIdle = idleSeconds >= _idleThresholdSeconds;

            if (_isIdle && !wasIdle)
                shouldRaiseStarted = true;
            else if (!_isIdle && wasIdle)
                shouldRaiseEnded = true;
        }

        if (shouldRaiseStarted)
            IdleStarted?.Invoke(this, EventArgs.Empty);

        if (shouldRaiseEnded)
            IdleEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handler for SystemEvents.SessionSwitch — raises Locked / Unlocked events.
    /// </summary>
    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                lock (_stateLock)
                {
                    _isLocked = true;
                }
                Locked?.Invoke(this, EventArgs.Empty);
                break;

            case SessionSwitchReason.SessionUnlock:
                lock (_stateLock)
                {
                    _isLocked = false;
                    // Reset idle state on unlock — user is back
                    _isIdle = false;
                }
                Unlocked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>
    /// Disposes the timer and unsubscribes from system events.
    /// </summary>
    public void Dispose()
    {
        Stop();
        _idleTimer.Dispose();
    }
}
