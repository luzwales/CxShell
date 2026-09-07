using System.Globalization;
using CxShell.Models;

namespace CxShell.Services;

/// <summary>
/// Parses the small Xshell-compatible launch surface used by bastion and SSO
/// integrations. It deliberately does not implement remote command options.
/// </summary>
public static class ExternalLaunchParser
{
    public static ExternalLaunchRequest? Parse(IReadOnlyList<string>? args)
        => TryParse(args, out var request, out _) ? request : null;

    public static bool TryParse(
        IReadOnlyList<string>? args,
        out ExternalLaunchRequest? request,
        out string? errorMessage)
    {
        request = null;
        errorMessage = null;
        if (args is null || args.Count == 0)
            return false;

        string? url = null;
        string? sessionFile = null;
        string? username = null;
        string? password = null;
        string? privateKeyPath = null;
        var port = 0;
        string? displayName = null;
        var hasExternalArguments = false;

        for (var index = 0; index < args.Count; index++)
        {
            var raw = Unquote(args[index]);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (!raw.StartsWith("-", StringComparison.Ordinal))
            {
                if (url is null && LooksLikeUrl(raw))
                {
                    url = raw;
                    hasExternalArguments = true;
                }

                continue;
            }

            var (name, inlineValue) = SplitOption(raw);
            switch (name)
            {
                case "-url":
                case "-encryurl":
                    hasExternalArguments = true;
                    url = Unquote(inlineValue ?? ReadValue(args, ref index));
                    break;

                case "-newtab":
                    hasExternalArguments = true;
                    var newTabValue = Unquote(inlineValue ?? ReadValue(args, ref index));
                    if (LooksLikeUrl(newTabValue))
                        url = newTabValue;
                    else if (!string.IsNullOrWhiteSpace(newTabValue))
                        displayName = newTabValue;
                    break;

                case "-f":
                case "-file":
                    hasExternalArguments = true;
                    sessionFile = Unquote(inlineValue ?? ReadValue(args, ref index));
                    break;

                case "-l":
                case "-user":
                    hasExternalArguments = true;
                    username ??= Unquote(inlineValue ?? ReadValue(args, ref index));
                    break;

                case "-p":
                case "-port":
                    hasExternalArguments = true;
                    var portText = Unquote(inlineValue ?? ReadValue(args, ref index));
                    if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) ||
                        port is < 1 or > 65535)
                    {
                        errorMessage = "External launch port must be between 1 and 65535.";
                    }
                    break;

                case "-pw":
                case "-password":
                    hasExternalArguments = true;
                    password ??= Unquote(inlineValue ?? ReadValue(args, ref index));
                    break;

                case "-i":
                case "-identity":
                    hasExternalArguments = true;
                    privateKeyPath ??= Unquote(inlineValue ?? ReadValue(args, ref index));
                    break;
            }
        }

        if (errorMessage is not null)
            return true;

        if (url is not null)
        {
            request = ParseUrl(url, IsProtocolInvocation(args));
        }
        else if (!string.IsNullOrWhiteSpace(sessionFile))
        {
            request = ParseSessionFile(sessionFile!, out errorMessage);
        }

        if (request is null)
        {
            if (hasExternalArguments)
                errorMessage ??= "External launch did not contain a valid ssh://, sftp://, or .xsh target.";
            return hasExternalArguments;
        }

        request = new ExternalLaunchRequest
        {
            Scheme = request.Scheme,
            Protocol = request.Protocol,
            IsSupported = request.IsSupported,
            Host = request.Host,
            Port = port > 0 ? port : request.Port,
            Username = string.IsNullOrWhiteSpace(username) ? request.Username : username,
            Password = string.IsNullOrEmpty(password) ? request.Password : password,
            PrivateKeyPath = string.IsNullOrWhiteSpace(privateKeyPath) ? request.PrivateKeyPath : privateKeyPath,
            InitialRemoteDirectory = request.InitialRemoteDirectory,
            DisplayName = displayName ?? request.DisplayName,
            Origin = request.Origin
        };
        return true;
    }

    public static ExternalLaunchRequest? ParseUrl(
        string? rawUrl,
        bool protocolInvocation = true)
    {
        var text = Unquote(rawUrl);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var scheme = "ssh";
        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd > 0)
        {
            scheme = text[..schemeEnd].Trim().ToLowerInvariant();
            text = text[(schemeEnd + 3)..];
        }

        if (!TrySplitAuthority(text, out var userInfo, out var authority, out var path) ||
            !TryParseHostPort(authority, out var host, out var explicitPort))
            return null;

        var username = string.Empty;
        string? password = null;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var colon = userInfo.IndexOf(':');
            username = Decode(colon >= 0 ? userInfo[..colon] : userInfo);
            if (colon >= 0)
                password = Decode(userInfo[(colon + 1)..]);
        }

        var (protocol, supported, defaultPort) = MapScheme(scheme);
        return new ExternalLaunchRequest
        {
            Scheme = scheme,
            Protocol = protocol,
            IsSupported = supported,
            Host = host,
            Port = explicitPort > 0 ? explicitPort : defaultPort,
            Username = username,
            Password = string.IsNullOrEmpty(password) ? null : password,
            InitialRemoteDirectory = DecodePath(path),
            Origin = protocolInvocation ? ExternalLaunchOrigin.UrlProtocol : ExternalLaunchOrigin.CommandLine
        };
    }

    private static ExternalLaunchRequest? ParseSessionFile(string path, out string? errorMessage)
    {
        errorMessage = null;
        if (!File.Exists(path))
        {
            errorMessage = $"Xshell session file was not found: {path}";
            return null;
        }

        try
        {
            using var reader = new StreamReader(path, System.Text.Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
            var section = string.Empty;
            string? host = null;
            string? protocol = null;
            string? user = null;
            string? keyPath = null;
            var port = 0;
            while (reader.ReadLine() is { } rawLine)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1];
                    continue;
                }

                var equals = line.IndexOf('=');
                if (equals <= 0)
                    continue;
                var key = line[..equals].Trim();
                var value = line[(equals + 1)..].Trim();
                if (section.Equals("CONNECTION", StringComparison.OrdinalIgnoreCase))
                {
                    if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) host = value;
                    else if (key.Equals("Protocol", StringComparison.OrdinalIgnoreCase)) protocol = value;
                    else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, out port);
                }
                else if (section.Equals("CONNECTION:AUTHENTICATION", StringComparison.OrdinalIgnoreCase))
                {
                    if (key.Equals("UserName", StringComparison.OrdinalIgnoreCase)) user = value;
                    else if (key.Equals("UserKey", StringComparison.OrdinalIgnoreCase)) keyPath = value;
                }
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                errorMessage = "The Xshell session file does not contain a host.";
                return null;
            }

            var scheme = string.IsNullOrWhiteSpace(protocol) ? "ssh" : protocol.Trim().ToLowerInvariant();
            var mapped = MapScheme(scheme);
            return new ExternalLaunchRequest
            {
                Scheme = scheme,
                Protocol = mapped.Protocol,
                IsSupported = mapped.Supported,
                Host = host.Trim(),
                Port = port is >= 1 and <= 65535 ? port : mapped.DefaultPort,
                Username = user?.Trim() ?? string.Empty,
                PrivateKeyPath = string.IsNullOrWhiteSpace(keyPath) ? null : keyPath.Trim(),
                DisplayName = Path.GetFileNameWithoutExtension(path),
                Origin = ExternalLaunchOrigin.SessionFile
            };
        }
        catch (IOException ex)
        {
            errorMessage = $"Could not read the Xshell session file: {ex.Message}";
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            errorMessage = $"Could not read the Xshell session file: {ex.Message}";
            return null;
        }
    }

    private static (SessionProtocol Protocol, bool Supported, int DefaultPort) MapScheme(string scheme)
        => scheme switch
        {
            "ssh" or "" => (SessionProtocol.SSH, true, 22),
            "sftp" => (SessionProtocol.SFTP, true, 22),
            "ftp" => (SessionProtocol.FTP, true, 21),
            "ftps" => (SessionProtocol.FTP, false, 21),
            "telnet" => (SessionProtocol.TELNET, false, 23),
            "rlogin" => (SessionProtocol.RLOGIN, false, 513),
            _ => (SessionProtocol.SSH, false, 22)
        };

    private static bool TryParseHostPort(string value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        value = value.Trim();
        if (value.StartsWith('['))
        {
            var close = value.IndexOf(']');
            if (close <= 1)
                return false;
            host = value[1..close];
            var rest = value[(close + 1)..];
            return rest.Length == 0 || rest[0] == ':' && int.TryParse(rest[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out port);
        }

        var colon = value.LastIndexOf(':');
        if (colon > 0 && value.IndexOf(':') == colon)
        {
            if (!int.TryParse(value[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)
                return false;
            host = value[..colon];
        }
        else
        {
            host = value;
        }

        return !string.IsNullOrWhiteSpace(host);
    }

    private static bool TrySplitAuthority(
        string text,
        out string userInfo,
        out string authority,
        out string? path)
    {
        var authorityStart = text.IndexOfAny(['/', '?', '#']);
        var beforePath = authorityStart >= 0 ? text[..authorityStart] : text;
        var pathStart = authorityStart >= 0 && text[authorityStart] == '/' ? authorityStart : -1;

        for (var at = beforePath.LastIndexOf('@'); at >= 0; at = at == 0 ? -1 : beforePath.LastIndexOf('@', at - 1))
        {
            var candidateAuthority = beforePath[(at + 1)..];
            if (LooksLikeHostPort(candidateAuthority))
            {
                userInfo = beforePath[..at];
                authority = candidateAuthority;
                path = pathStart >= 0 ? text[pathStart..] : null;
                return true;
            }
        }

        if (LooksLikeHostPort(beforePath))
        {
            userInfo = string.Empty;
            authority = beforePath;
            path = pathStart >= 0 ? text[pathStart..] : null;
            return true;
        }

        for (var at = text.LastIndexOf('@'); at >= 0; at = at == 0 ? -1 : text.LastIndexOf('@', at - 1))
        {
            var right = text[(at + 1)..];
            var rightEnd = right.IndexOfAny(['/', '?', '#']);
            var candidateAuthority = rightEnd >= 0 ? right[..rightEnd] : right;
            if (LooksLikeHostPort(candidateAuthority))
            {
                userInfo = text[..at];
                authority = candidateAuthority;
                path = rightEnd >= 0 && right[rightEnd] == '/' ? right[rightEnd..] : null;
                return true;
            }
        }

        userInfo = string.Empty;
        authority = string.Empty;
        path = null;
        return false;
    }

    private static bool LooksLikeHostPort(string value)
    {
        if (!TryParseHostPort(value, out var host, out _))
            return false;

        var ipv6 = host.Contains(':', StringComparison.Ordinal);
        foreach (var character in host)
        {
            var accepted = ipv6
                ? char.IsAsciiHexDigit(character) || character is ':' or '.' or '%'
                : char.IsLetterOrDigit(character) || character is '.' or '-' or '_';
            if (!accepted)
                return false;
        }

        return host.Length > 0;
    }

    private static string? DecodePath(string? path)
        => string.IsNullOrWhiteSpace(path) || path == "/" ? null : Decode(path);

    private static string Decode(string value)
    {
        try { return Uri.UnescapeDataString(value); }
        catch (UriFormatException) { return value; }
    }

    private static bool LooksLikeUrl(string value)
        => value.Contains("://", StringComparison.Ordinal)
           || value.Contains('@', StringComparison.Ordinal) && !value.Contains(' ', StringComparison.Ordinal);

    private static bool IsProtocolInvocation(IReadOnlyList<string> args)
        => args.Count <= 2 && (args.Count == 1 || args[0].Equals("-url", StringComparison.OrdinalIgnoreCase));

    private static string ReadValue(IReadOnlyList<string> args, ref int index)
        => index + 1 < args.Count && !args[index + 1].StartsWith("-", StringComparison.Ordinal)
            ? args[++index]
            : string.Empty;

    private static (string Name, string? Inline) SplitOption(string value)
    {
        var equals = value.IndexOf('=');
        return equals > 0
            ? (value[..equals].ToLowerInvariant(), value[(equals + 1)..])
            : (value.ToLowerInvariant(), null);
    }

    private static string Unquote(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length >= 2 && ((normalized[0] == '"' && normalized[^1] == '"') || (normalized[0] == '\'' && normalized[^1] == '\''))
            ? normalized[1..^1].Trim()
            : normalized;
    }
}
