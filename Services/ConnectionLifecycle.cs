using System.Threading;

namespace CxShell.Services;

/// <summary>
/// Tracks the active instance of a reusable connection service. A late callback
/// from an older instance must not be able to close or mutate the newer one.
/// </summary>
internal sealed class ConnectionLifecycle
{
    private long _nextInstanceId;
    private long _activeInstanceId;
    private int _closedRaised = 1;

    public long Begin()
    {
        var instanceId = Interlocked.Increment(ref _nextInstanceId);
        Volatile.Write(ref _activeInstanceId, instanceId);
        Volatile.Write(ref _closedRaised, 0);
        return instanceId;
    }

    public void Invalidate()
    {
        var invalidatedInstanceId = Interlocked.Increment(ref _nextInstanceId);
        Volatile.Write(ref _activeInstanceId, invalidatedInstanceId);
        Volatile.Write(ref _closedRaised, 1);
    }

    public long ActiveInstanceId => Volatile.Read(ref _activeInstanceId);

    public bool TryClaimClosed(long instanceId)
    {
        if (instanceId == 0 ||
            instanceId != Volatile.Read(ref _activeInstanceId))
            return false;

        return Interlocked.Exchange(ref _closedRaised, 1) == 0;
    }
}
