using System.Text.Json.Serialization;

namespace FxShell.Models;

/// <summary>Describes where an external launch request came from.</summary>
public enum ExternalLaunchOrigin
{
    CommandLine,
    UrlProtocol,
    SessionFile
}

/// <summary>
/// A connection request supplied by another application. Credentials are
/// intentionally transient and are never included in the diagnostic text.
/// </summary>
public sealed class ExternalLaunchRequest
{
    public string Scheme { get; init; } = "ssh";
    public SessionProtocol Protocol { get; init; } = SessionProtocol.SSH;
    public bool IsSupported { get; init; } = true;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 22;
    public string Username { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string? PrivateKeyPath { get; init; }
    public string? InitialRemoteDirectory { get; init; }
    public string? DisplayName { get; init; }
    public ExternalLaunchOrigin Origin { get; init; } = ExternalLaunchOrigin.CommandLine;

    [JsonIgnore]
    public string TrustKey =>
        $"{Scheme.ToLowerInvariant()}://{Username}@{Host.ToLowerInvariant()}:{Port}";

    [JsonIgnore]
    public string TargetText => string.IsNullOrWhiteSpace(Username)
        ? $"{Host}:{Port}"
        : $"{Username}@{Host}:{Port}";

    [JsonIgnore]
    public bool HasCredential => !string.IsNullOrEmpty(Password) || !string.IsNullOrWhiteSpace(PrivateKeyPath);

    public SessionInfo CreateSession()
    {
        var protocol = Protocol;
        return new SessionInfo
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(DisplayName) ? TargetText : DisplayName.Trim(),
            Protocol = protocol,
            Host = Host,
            Port = Port,
            Username = Username,
            AuthMethod = PrivateKeyPath is null ? AuthMethod.Password : AuthMethod.PrivateKey,
            PrivateKeyPath = PrivateKeyPath,
            SshAutoOpenSftpPanel = false,
            SshAutoOpenMonitorPanel = false,
            SshDoNotStartFileManager = true,
            SftpRemoteStartDirectory = protocol is SessionProtocol.SFTP or SessionProtocol.FTP
                ? InitialRemoteDirectory ?? string.Empty
                : string.Empty,
            AdvancedFtpPort = protocol == SessionProtocol.FTP ? Port : 21
        };
    }

    public override string ToString()
    {
        var credentialText = HasCredential ? "supplied" : "none";
        return $"{Scheme}://{TargetText} (origin={Origin}, credentials={credentialText})";
    }
}
