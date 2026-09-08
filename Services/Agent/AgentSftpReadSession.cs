using FxShell.Models;

namespace FxShell.Services.Agent;

/// <summary>
/// A per-tab, lazy SFTP connection used only by Agent read-only tools. It is
/// deliberately separate from the visible SFTP panel so Agent inspection does
/// not change the user's current directory or interrupt a file transfer.
/// </summary>
public sealed class AgentSftpReadSession : IDisposable, IAsyncDisposable
{
    private readonly SftpService _service = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private string? _connectionKey;
    private int _disposed;

    public bool IsConnected => _service.IsConnected;

    public async Task<IReadOnlyList<SftpFileItem>> ListDirectoryAsync(
        SessionInfo session,
        string? password,
        string path,
        CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(
            session,
            password,
            (service, token) => service.ListDirectoryAsync(path, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ReadTextFileAsync(
        SessionInfo session,
        string? password,
        string path,
        int maxCharacters,
        CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(
            session,
            password,
            (service, token) => service.ReadTextFileForAgentAsync(path, maxCharacters, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SftpFileItem> StatAsync(
        SessionInfo session,
        string? password,
        string path,
        CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(
            session,
            password,
            (service, token) => service.GetItemForAgentAsync(path, token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> WithConnectionAsync<T>(
        SessionInfo session,
        string? password,
        Func<SftpService, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(operation);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        var operationCancellation = linkedCancellation.Token;
        await _gate.WaitAsync(operationCancellation).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            operationCancellation.ThrowIfCancellationRequested();
            var connectionKey = BuildConnectionKey(session);
            if (!_service.IsConnected || !string.Equals(_connectionKey, connectionKey, StringComparison.Ordinal))
            {
                _service.Disconnect();
                _connectionKey = null;
                await _service.ConnectAsync(session, password, operationCancellation).ConfigureAwait(false);
                _connectionKey = connectionKey;
            }

            operationCancellation.ThrowIfCancellationRequested();
            using var cancellationRegistration = operationCancellation.Register(
                static state => ((SftpService)state!).Disconnect(),
                _service);
            return await operation(_service, operationCancellation).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string BuildConnectionKey(SessionInfo session)
        => string.Join('|',
            session.Host?.Trim() ?? string.Empty,
            session.Port,
            session.Username?.Trim() ?? string.Empty,
            session.AuthMethod,
            session.PrivateKeyPath?.Trim() ?? string.Empty,
            session.SelectedProxyId,
            session.Proxy.Protocol,
            session.Proxy.Host?.Trim() ?? string.Empty,
            session.Proxy.Port);

    public void Dispose()
        => _ = DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _lifetimeCancellation.Cancel();
        try
        {
            _service.Disconnect();
        }
        finally
        {
            _connectionKey = null;
        }

        try
        {
            await _gate.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The transport was already disconnected; do not block tab close
            // indefinitely on a third-party SFTP implementation.
        }
        finally
        {
            _service.Dispose();
            _gate.Dispose();
            _lifetimeCancellation.Dispose();
        }
    }
}
