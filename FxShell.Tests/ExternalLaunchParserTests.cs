using System.Text;
using FxShell.Models;
using FxShell.Services;

namespace FxShell.Tests;

public sealed class ExternalLaunchParserTests
{
    [Fact]
    public void ParseUrl_ReadsSshCredentialsAndIpv6WithoutLeakingPassword()
    {
        var request = ExternalLaunchParser.Parse(["ssh://ops:p@ss@[2001:db8::10]:2222"]);

        Assert.NotNull(request);
        Assert.Equal("2001:db8::10", request.Host);
        Assert.Equal(2222, request.Port);
        Assert.Equal("ops", request.Username);
        Assert.Equal("p@ss", request.Password);
        Assert.DoesNotContain("p@ss", request.ToString());
    }

    [Fact]
    public void ParseUrl_AllowsUnescapedPasswordSeparatorsBeforeTheHost()
    {
        var request = ExternalLaunchParser.ParseUrl("ssh://ops:pa/ss#word@server.test:22");

        Assert.NotNull(request);
        Assert.Equal("server.test", request.Host);
        Assert.Equal("pa/ss#word", request.Password);
    }

    [Fact]
    public void ParseUrl_DoesNotTreatAnAtSignInTheRemotePathAsTheHostSeparator()
    {
        var request = ExternalLaunchParser.ParseUrl("sftp://server.test:22/home/ops@corp");

        Assert.NotNull(request);
        Assert.Equal("server.test", request.Host);
        Assert.Equal("/home/ops@corp", request.InitialRemoteDirectory);
        Assert.Empty(request.Username);
    }

    [Fact]
    public void Parse_ExplicitXshellArgumentsOverrideUrlValues()
    {
        var request = ExternalLaunchParser.Parse(
            ["-url", "ssh://url-user:url-password@example.test:22", "-l", "cli-user", "-p", "2200", "-pw", "cli-password", "-i", "C:\\keys\\id"]);

        Assert.NotNull(request);
        Assert.Equal(2200, request.Port);
        Assert.Equal("cli-user", request.Username);
        Assert.Equal("cli-password", request.Password);
        Assert.Equal("C:\\keys\\id", request.PrivateKeyPath);
    }

    [Fact]
    public void Parse_XshellFileReadsUtf16ConnectionFieldsAndNeverReadsPassword()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "jump.xsh");
        File.WriteAllText(
            path,
            "[SessionInfo]\r\nVersion=7.0\r\n[CONNECTION]\r\nHost=server.test\r\nPort=2201\r\nProtocol=SSH\r\n[CONNECTION:AUTHENTICATION]\r\nUserName=deploy\r\nPassword=encrypted-value\r\nUserKey=C:\\keys\\deploy\r\n",
            Encoding.Unicode);

        var request = ExternalLaunchParser.Parse(["-f", path]);

        Assert.NotNull(request);
        Assert.Equal(ExternalLaunchOrigin.SessionFile, request.Origin);
        Assert.Equal("server.test", request.Host);
        Assert.Equal(2201, request.Port);
        Assert.Equal("deploy", request.Username);
        Assert.Null(request.Password);
        Assert.Equal("C:\\keys\\deploy", request.PrivateKeyPath);
        Assert.DoesNotContain("encrypted-value", request.ToString());
    }

    [Fact]
    public void Parse_UnsupportedProtocolIsReportedWithoutCreatingConnection()
    {
        var found = ExternalLaunchParser.TryParse(
            ["ssh+custom://user@example.test:22"],
            out var request,
            out var error);

        Assert.True(found);
        Assert.NotNull(request);
        Assert.False(request.IsSupported);
        Assert.Null(error);
    }

    [Fact]
    public void Parse_DoesNotTreatRemoteCommandSwitchesAsSupportedInput()
    {
        var request = ExternalLaunchParser.Parse(["-url", "ssh://user@example.test:22", "-e", "rm -rf /"]);

        Assert.NotNull(request);
        Assert.Equal(ExternalLaunchOrigin.CommandLine, request.Origin);
        Assert.Equal("example.test", request.Host);
    }

    [Fact]
    public void CommandLineOptions_ExposeTheUnifiedExternalRequest()
    {
        var options = CommandLineLaunchOptions.Parse(["-url", "sftp://ops@example.test:22/home/ops"]);

        Assert.True(options.HasCommand);
        Assert.NotNull(options.ExternalRequest);
        Assert.Equal(SessionProtocol.SFTP, options.ExternalRequest.Protocol);
        Assert.Equal("/home/ops", options.ExternalRequest.InitialRemoteDirectory);
        Assert.NotNull(options.SessionRequest);
    }

    [Fact]
    public void TokenPayload_IsConvertedToTheSameExternalSecurityPath()
    {
        var parent = CommandLineLaunchOptions.Parse(["-token", "opaque-token"]);
        var resolved = CommandLineLaunchOptions.ParseTokenPayload(
            "{\"host\":\"example.test\",\"username\":\"ops\",\"port\":22,\"password\":\"temporary\"}",
            parent);

        Assert.NotNull(resolved.ExternalRequest);
        Assert.Equal("temporary", resolved.ExternalRequest.Password);
        Assert.NotNull(resolved.SessionRequest);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fxshell-external-launch-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best effort cleanup for test files.
            }
        }
    }
}
