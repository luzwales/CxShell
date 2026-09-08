using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FxShell.Services;

namespace FxShell.Tests;

public sealed class ConnectionLifecycleTests
{
    [Fact]
    public void LateInstanceCannotClaimCloseForNewInstance()
    {
        var lifecycle = new ConnectionLifecycle();
        var oldInstance = lifecycle.Begin();
        var newInstance = lifecycle.Begin();

        Assert.False(lifecycle.TryClaimClosed(oldInstance));
        Assert.True(lifecycle.TryClaimClosed(newInstance));
        Assert.False(lifecycle.TryClaimClosed(newInstance));
    }

    [Fact]
    public void InvalidateRejectsCallbacksFromTheInvalidatedInstance()
    {
        var lifecycle = new ConnectionLifecycle();
        var instance = lifecycle.Begin();

        lifecycle.Invalidate();

        Assert.False(lifecycle.TryClaimClosed(instance));
        Assert.False(lifecycle.TryClaimClosed(lifecycle.ActiveInstanceId));
    }

    [Fact]
    public async Task ConcurrentCloseCallbacksCanClaimOnlyOnce()
    {
        var lifecycle = new ConnectionLifecycle();
        var instance = lifecycle.Begin();
        var claims = new ConcurrentBag<bool>();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            claims.Add(lifecycle.TryClaimClosed(instance)))));

        Assert.Single(claims, claimed => claimed);
    }
}
