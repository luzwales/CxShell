using FxShell.Models;

namespace FxShell.Services.Agent;

public sealed record AgentGatewayCapabilities
{
    public bool SupportsSessionDiscovery { get; init; } = true;
    public bool SupportsSavedSessionManagement { get; init; }
    public bool RequiresApprovalForSessionOpen { get; init; }
    public bool SupportsTerminalCommandDispatch { get; init; } = true;
    public bool SupportsCommandOutputCapture { get; init; }
    public bool SupportsSftpRead { get; init; }
    public bool SupportsReadOnlyDiagnostics { get; init; }
    public bool AllowsCommandExecution { get; init; } = true;
    public string PermissionMode { get; init; } = AgentPermissionPolicy.RiskBasedApprovalMode;
    public bool RequiresApprovalForDangerousCommands { get; init; } = true;
    public bool RequiresApprovalForChangeCommands { get; init; }
    public bool ReadOnlyMode { get; init; }
    public bool HasCommandAllowList { get; init; }
    public bool HasCommandBlockList { get; init; }
    public IReadOnlyList<string> SupportedProtocols { get; init; } = ["SSH"];
}

public interface IAgentSessionGateway
{
    AgentGatewayCapabilities Capabilities { get; }
    IReadOnlyList<AgentSessionSnapshot> GetSessions();
    AgentSessionSnapshot? GetSession(Guid sessionId);
    bool TryGetTerminalText(Guid sessionId, out string terminalText);
    bool TryGetWorkingDirectory(Guid sessionId, out string workingDirectory);
    Task<AgentRemoteDirectoryResult> ListRemoteDirectoryAsync(
        Guid sessionId,
        string path,
        int maxEntries,
        CancellationToken cancellationToken = default);
    Task<AgentRemoteFileReadResult> ReadRemoteFileAsync(
        Guid sessionId,
        string path,
        int maxCharacters,
        CancellationToken cancellationToken = default);
    Task<AgentRemotePathResult> StatRemotePathAsync(
        Guid sessionId,
        string path,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentSavedSessionSnapshot>> ListSavedSessionsAsync(
        CancellationToken cancellationToken = default);
    Task<AgentSessionOpenResult> OpenSavedSessionAsync(
        AgentSessionOpenRequest request,
        CancellationToken cancellationToken = default);
    Task<AgentSessionCloseResult> CloseAgentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
    /// <summary>Removes Agent ownership after the host closes a tab.</summary>
    void NotifySessionClosed(Guid sessionId);
    Task<AgentCommandResult> ExecuteCommandAsync(
        AgentCommandRequest request,
        CancellationToken cancellationToken = default,
        Action<AgentCommandProgress>? progressReceived = null);
    Task<AgentFleetInspectionResult> RunReadOnlyDiagnosticAcrossSessionsAsync(
        string scope,
        CancellationToken cancellationToken = default);
    bool TryCancel(Guid requestId);
    bool TryApprove(Guid requestId, out string approvalToken);
    bool TryDeny(Guid requestId);
    IReadOnlyList<AgentAuditEntry> ReadAudit(int limit = AgentAuditLog.MaximumEntries);
}

public interface IAgentSessionHost
{
    IReadOnlyList<IAgentSessionEndpoint> GetAgentSessionEndpoints();
}

/// <summary>
/// Optional host boundary for Agent-managed connections. The Agent receives
/// only metadata and a runtime session id; the host remains responsible for
/// locating saved configuration, prompting the user, and creating a visible
/// tab. Implementations must never return credentials.
/// </summary>
public interface IAgentSessionLifecycleHost
{
    bool SupportsSavedSessionManagement { get; }
    Task<IReadOnlyList<AgentSavedSessionSnapshot>> ListSavedSessionsAsync(
        CancellationToken cancellationToken = default);
    Task<AgentSessionOpenResult> OpenSavedSessionAsync(
        AgentSessionOpenRequest request,
        CancellationToken cancellationToken = default);
    Task<AgentSessionCloseResult> CloseAgentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed record AgentSavedSessionSnapshot
{
    public Guid SavedSessionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public SessionProtocol Protocol { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string Username { get; init; } = string.Empty;
    public bool IsOpen { get; init; }
    public Guid? OpenSessionId { get; init; }
}

public sealed record AgentSessionOpenRequest(
    Guid SavedSessionId,
    string Reason,
    bool ReuseConnected = true);

public enum AgentSessionOpenStatus
{
    Opened,
    UserDenied,
    UserCancelled,
    NotFound,
    UnsupportedProtocol,
    ConnectionFailed,
    Unsupported
}

public sealed record AgentSessionOpenResult(
    AgentSessionOpenStatus Status,
    AgentSessionSnapshot? Session = null,
    string? Error = null,
    bool AgentOwned = false)
{
    public bool Opened => Status == AgentSessionOpenStatus.Opened && Session != null;
}

public enum AgentSessionCloseStatus
{
    Closed,
    NotFound,
    NotAgentOwned,
    Unsupported
}

public sealed record AgentSessionCloseResult(
    AgentSessionCloseStatus Status,
    string? Error = null)
{
    public bool Closed => Status == AgentSessionCloseStatus.Closed;
}

public interface IAgentSessionEndpoint
{
    AgentSessionSnapshot Snapshot { get; }
    bool SupportsCommandOutputCapture { get; }
    bool SupportsTerminalText { get; }
    string GetTerminalText();
    bool SupportsWorkingDirectory => false;
    string? GetWorkingDirectory() => null;
    bool SupportsSftpRead => false;
    Task<AgentRemoteDirectoryResult> ListRemoteDirectoryAsync(
        string path,
        int maxEntries,
        CancellationToken cancellationToken)
        => Task.FromResult(AgentRemoteDirectoryResult.Unsupported(path));
    Task<AgentRemoteFileReadResult> ReadRemoteFileAsync(
        string path,
        int maxCharacters,
        CancellationToken cancellationToken)
        => Task.FromResult(AgentRemoteFileReadResult.Unsupported(path));
    Task<AgentRemotePathResult> StatRemotePathAsync(
        string path,
        CancellationToken cancellationToken)
        => Task.FromResult(AgentRemotePathResult.Unsupported(path));
    Task SendCommandAsync(AgentCommandRequest request, CancellationToken cancellationToken);
    Task<AgentCommandExecutionResult> ExecuteCommandAsync(
        AgentCommandRequest request,
        CancellationToken cancellationToken);

    Task<AgentCommandExecutionResult> ExecuteCommandAsync(
        AgentCommandRequest request,
        CancellationToken cancellationToken,
        Action<AgentCommandProgress>? progressReceived)
        => ExecuteCommandAsync(request, cancellationToken);
}

public sealed record AgentRemoteFileEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTime LastModified,
    string Permissions,
    bool IsSymbolicLink);

public sealed record AgentRemoteDirectoryResult(
    bool Success,
    string Path,
    IReadOnlyList<AgentRemoteFileEntry> Entries,
    bool Truncated = false,
    string? Error = null,
    string? ErrorCode = null)
{
    public static AgentRemoteDirectoryResult Unsupported(string path)
        => new(false, path, [], Error: "SFTP read access is not available for this session.", ErrorCode: "sftp_unsupported");
}

public sealed record AgentRemoteFileReadResult(
    bool Success,
    string Path,
    string Content,
    int Characters,
    string? Error = null,
    string? ErrorCode = null)
{
    public static AgentRemoteFileReadResult Unsupported(string path)
        => new(false, path, string.Empty, 0, "SFTP read access is not available for this session.", "sftp_unsupported");
}

public sealed record AgentRemotePathResult(
    bool Success,
    string Path,
    AgentRemoteFileEntry? Entry = null,
    string? Error = null,
    string? ErrorCode = null)
{
    public static AgentRemotePathResult Unsupported(string path)
        => new(false, path, Error: "SFTP read access is not available for this session.", ErrorCode: "sftp_unsupported");
}

public sealed record AgentCommandExecutionResult(
    bool RemoteCompletionConfirmed,
    string? Output = null,
    string? Error = null,
    int? ExitCode = null)
{
    public bool Succeeded => RemoteCompletionConfirmed && (ExitCode is null or 0);
}

public sealed class AgentSessionEndpoint : IAgentSessionEndpoint
{
    private readonly Func<AgentSessionSnapshot> _snapshotProvider;
    private readonly Func<AgentCommandRequest, CancellationToken, Task> _sendCommand;
    private readonly Func<AgentCommandRequest, CancellationToken, Task<string>>? _runCommand;
    private readonly Func<AgentCommandRequest, CancellationToken, Task<AgentCommandExecutionResult>>? _runCommandResult;
    private readonly Func<AgentCommandRequest, CancellationToken, Action<AgentCommandProgress>?, Task<AgentCommandExecutionResult>>? _runCommandProgressResult;
    private readonly Func<string>? _terminalTextProvider;
    private readonly Func<string?>? _workingDirectoryProvider;
    private readonly Func<string, int, CancellationToken, Task<AgentRemoteDirectoryResult>>? _remoteDirectoryProvider;
    private readonly Func<string, int, CancellationToken, Task<AgentRemoteFileReadResult>>? _remoteFileProvider;
    private readonly Func<string, CancellationToken, Task<AgentRemotePathResult>>? _remotePathProvider;

    public AgentSessionEndpoint(
        Func<AgentSessionSnapshot> snapshotProvider,
        Func<AgentCommandRequest, CancellationToken, Task> sendCommand,
        Func<AgentCommandRequest, CancellationToken, Task<string>>? runCommand = null)
        : this(snapshotProvider, sendCommand, runCommand, runCommandResult: null)
    {
    }

    public AgentSessionEndpoint(
        Func<AgentSessionSnapshot> snapshotProvider,
        Func<AgentCommandRequest, CancellationToken, Task> sendCommand,
        Func<AgentCommandRequest, CancellationToken, Task<string>>? runCommand,
        Func<AgentCommandRequest, CancellationToken, Task<AgentCommandExecutionResult>>? runCommandResult)
        : this(
            snapshotProvider,
            sendCommand,
            runCommand,
            runCommandResult,
            runCommandProgressResult: null)
    {
    }

    public AgentSessionEndpoint(
        Func<AgentSessionSnapshot> snapshotProvider,
        Func<AgentCommandRequest, CancellationToken, Task> sendCommand,
        Func<AgentCommandRequest, CancellationToken, Task<string>>? runCommand,
        Func<AgentCommandRequest, CancellationToken, Task<AgentCommandExecutionResult>>? runCommandResult,
        Func<AgentCommandRequest, CancellationToken, Action<AgentCommandProgress>?, Task<AgentCommandExecutionResult>>? runCommandProgressResult,
        Func<string>? terminalTextProvider = null,
        Func<string, int, CancellationToken, Task<AgentRemoteDirectoryResult>>? remoteDirectoryProvider = null,
        Func<string, int, CancellationToken, Task<AgentRemoteFileReadResult>>? remoteFileProvider = null,
        Func<string, CancellationToken, Task<AgentRemotePathResult>>? remotePathProvider = null,
        Func<string?>? workingDirectoryProvider = null)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _sendCommand = sendCommand ?? throw new ArgumentNullException(nameof(sendCommand));
        _runCommand = runCommand;
        _runCommandResult = runCommandResult;
        _runCommandProgressResult = runCommandProgressResult;
        _terminalTextProvider = terminalTextProvider;
        _remoteDirectoryProvider = remoteDirectoryProvider;
        _remoteFileProvider = remoteFileProvider;
        _remotePathProvider = remotePathProvider;
        _workingDirectoryProvider = workingDirectoryProvider;
    }

    public AgentSessionSnapshot Snapshot => _snapshotProvider();
    public bool SupportsCommandOutputCapture => _runCommand != null ||
                                                _runCommandResult != null ||
                                                _runCommandProgressResult != null;

    public bool SupportsTerminalText => _terminalTextProvider != null;

    public string GetTerminalText()
        => _terminalTextProvider?.Invoke() ?? string.Empty;

    public bool SupportsWorkingDirectory => _workingDirectoryProvider != null;

    public string? GetWorkingDirectory()
        => _workingDirectoryProvider?.Invoke();

    public bool SupportsSftpRead => _remoteDirectoryProvider != null &&
                                    _remoteFileProvider != null &&
                                    _remotePathProvider != null;

    public Task<AgentRemoteDirectoryResult> ListRemoteDirectoryAsync(
        string path,
        int maxEntries,
        CancellationToken cancellationToken)
        => _remoteDirectoryProvider?.Invoke(path, maxEntries, cancellationToken) ??
           Task.FromResult(AgentRemoteDirectoryResult.Unsupported(path));

    public Task<AgentRemoteFileReadResult> ReadRemoteFileAsync(
        string path,
        int maxCharacters,
        CancellationToken cancellationToken)
        => _remoteFileProvider?.Invoke(path, maxCharacters, cancellationToken) ??
           Task.FromResult(AgentRemoteFileReadResult.Unsupported(path));

    public Task<AgentRemotePathResult> StatRemotePathAsync(
        string path,
        CancellationToken cancellationToken)
        => _remotePathProvider?.Invoke(path, cancellationToken) ??
           Task.FromResult(AgentRemotePathResult.Unsupported(path));

    public Task SendCommandAsync(AgentCommandRequest request, CancellationToken cancellationToken)
        => _sendCommand(request, cancellationToken);

    public async Task<AgentCommandExecutionResult> ExecuteCommandAsync(
        AgentCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (_runCommandProgressResult != null)
        {
            return await _runCommandProgressResult(request, cancellationToken, null)
                .ConfigureAwait(false);
        }

        if (_runCommandResult != null)
            return await _runCommandResult(request, cancellationToken).ConfigureAwait(false);

        if (_runCommand == null)
        {
            await _sendCommand(request, cancellationToken).ConfigureAwait(false);
            return new AgentCommandExecutionResult(false);
        }

        var output = await _runCommand(request, cancellationToken).ConfigureAwait(false);
        return new AgentCommandExecutionResult(true, output);
    }

    public async Task<AgentCommandExecutionResult> ExecuteCommandAsync(
        AgentCommandRequest request,
        CancellationToken cancellationToken,
        Action<AgentCommandProgress>? progressReceived)
    {
        if (_runCommandProgressResult != null)
            return await _runCommandProgressResult(request, cancellationToken, progressReceived)
                .ConfigureAwait(false);

        return await ExecuteCommandAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DelegateAgentSessionHost : IAgentSessionHost, IAgentSessionLifecycleHost
{
    private readonly Func<IReadOnlyList<IAgentSessionEndpoint>> _endpointProvider;
    private readonly Func<CancellationToken, Task<IReadOnlyList<AgentSavedSessionSnapshot>>>? _savedSessionProvider;
    private readonly Func<AgentSessionOpenRequest, CancellationToken, Task<AgentSessionOpenResult>>? _openSession;
    private readonly Func<Guid, CancellationToken, Task<AgentSessionCloseResult>>? _closeSession;

    public DelegateAgentSessionHost(
        Func<IReadOnlyList<IAgentSessionEndpoint>> endpointProvider,
        Func<CancellationToken, Task<IReadOnlyList<AgentSavedSessionSnapshot>>>? savedSessionProvider = null,
        Func<AgentSessionOpenRequest, CancellationToken, Task<AgentSessionOpenResult>>? openSession = null,
        Func<Guid, CancellationToken, Task<AgentSessionCloseResult>>? closeSession = null)
    {
        _endpointProvider = endpointProvider ?? throw new ArgumentNullException(nameof(endpointProvider));
        _savedSessionProvider = savedSessionProvider;
        _openSession = openSession;
        _closeSession = closeSession;
    }

    public IReadOnlyList<IAgentSessionEndpoint> GetAgentSessionEndpoints()
        => _endpointProvider();

    public bool SupportsSavedSessionManagement => _savedSessionProvider != null &&
                                                   _openSession != null &&
                                                   _closeSession != null;

    public Task<IReadOnlyList<AgentSavedSessionSnapshot>> ListSavedSessionsAsync(
        CancellationToken cancellationToken = default)
        => _savedSessionProvider?.Invoke(cancellationToken) ??
           Task.FromResult<IReadOnlyList<AgentSavedSessionSnapshot>>([]);

    public Task<AgentSessionOpenResult> OpenSavedSessionAsync(
        AgentSessionOpenRequest request,
        CancellationToken cancellationToken = default)
        => _openSession?.Invoke(request, cancellationToken) ?? Task.FromResult(
            new AgentSessionOpenResult(
                AgentSessionOpenStatus.Unsupported,
                Error: "The host does not support Agent-managed saved sessions."));

    public Task<AgentSessionCloseResult> CloseAgentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => _closeSession?.Invoke(sessionId, cancellationToken) ?? Task.FromResult(
            new AgentSessionCloseResult(
                AgentSessionCloseStatus.Unsupported,
                "The host does not support Agent-managed saved sessions."));
}

public sealed class AgentCommandDeliveryException : Exception
{
    public AgentCommandDeliveryException(AgentCommandStatus status, string message)
        : base(message)
    {
        Status = status;
    }

    public AgentCommandStatus Status { get; }
}
