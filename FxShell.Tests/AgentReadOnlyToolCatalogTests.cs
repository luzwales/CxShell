using System.Text;
using System.Text.Json;
using FxShell.Models;
using FxShell.Services.Agent;

namespace FxShell.Tests;

public sealed class AgentReadOnlyToolCatalogTests
{
    [Fact]
    public void LinuxLogPlanValidatesSourceAndLineLimit()
    {
        var session = CreateSession("Linux/Unix");

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.LogsToolName,
            Json(new { source = "security", lines = 25 }),
            out var plan,
            out var error), error);

        Assert.Contains("tail -n 25", plan.Command, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", plan.Command, StringComparison.OrdinalIgnoreCase);
        Assert.False(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.LogsToolName,
            Json(new { source = "custom", lines = 25 }),
            out _,
            out error));
        Assert.Contains("source", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PortAndServicePlansDoNotAcceptUnboundedInput()
    {
        var session = CreateSession("Linux");

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.PortCheckToolName,
            Json(new { port = 2222 }),
            out var portPlan,
            out var error), error);
        Assert.Contains("2222", portPlan.Command, StringComparison.Ordinal);

        Assert.False(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.PortCheckToolName,
            Json(new { port = 70000 }),
            out _,
            out error));
        Assert.Contains("65535", error, StringComparison.Ordinal);

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.ServiceDetailToolName,
            Json(new { service = "sshd.service" }),
            out var servicePlan,
            out error), error);
        Assert.Contains("sshd.service", servicePlan.Command, StringComparison.Ordinal);

        Assert.False(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.ServiceDetailToolName,
            Json(new { service = "sshd; reboot" }),
            out _,
            out error));
        Assert.Contains("service", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowsPlansUseEncodedPowerShellAndKnownFileTargets()
    {
        var session = CreateSession("Windows");

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.PortCheckToolName,
            Json(new { port = 15900 }),
            out var portPlan,
            out var error), error);
        var script = DecodePowerShell(portPlan.Command);
        Assert.Contains("LocalPort 15900", script, StringComparison.Ordinal);
        Assert.Contains("-NoProfile", portPlan.Command, StringComparison.Ordinal);

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.FilePreviewToolName,
            Json(new { target = "hosts", lines = 20 }),
            out var filePlan,
            out error), error);
        Assert.Contains("C:\\Windows\\System32\\drivers\\etc\\hosts", DecodePowerShell(filePlan.Command), StringComparison.Ordinal);

        Assert.False(AgentReadOnlyToolCatalog.TryCreatePlan(
            session,
            AgentReadOnlyToolCatalog.FilePreviewToolName,
            Json(new { target = "os-release" }),
            out _,
            out error));
        Assert.Contains("not available", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemotePathToolsAreBoundedAndRejectCredentialPaths()
    {
        var linux = CreateSession("Linux/Unix");

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            linux,
            AgentReadOnlyToolCatalog.WorkingDirectoryToolName,
            JsonDocument.Parse("{}").RootElement,
            out var workingDirectory,
            out var error), error);
        Assert.Contains("pwd -P", workingDirectory.Command, StringComparison.Ordinal);

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            linux,
            AgentReadOnlyToolCatalog.StatRemotePathToolName,
            Json(new { path = "/var/log/nginx/access.log" }),
            out var stat,
            out error), error);
        Assert.Contains("stat --", stat.Command, StringComparison.Ordinal);
        Assert.Contains("/var/log/nginx/access.log", stat.Command, StringComparison.Ordinal);

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            linux,
            AgentReadOnlyToolCatalog.ReadRemoteFileToolName,
            Json(new { path = "/etc/nginx/nginx.conf", lines = 25 }),
            out var read,
            out error), error);
        Assert.Contains("sed -n '1,25p'", read.Command, StringComparison.Ordinal);

        Assert.False(AgentReadOnlyToolCatalog.TryCreatePlan(
            linux,
            AgentReadOnlyToolCatalog.ReadRemoteFileToolName,
            Json(new { path = "/home/operator/.ssh/id_ed25519" }),
            out _,
            out error));
        Assert.Contains("private keys", error, StringComparison.OrdinalIgnoreCase);

        Assert.False(AgentReadOnlyToolCatalog.TryCreatePlan(
            linux,
            AgentReadOnlyToolCatalog.ReadRemoteFileToolName,
            Json(new { path = "/etc/hosts", lines = 401 }),
            out _,
            out error));
        Assert.Contains("400", error, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsRemotePathToolsUseLiteralPathPowerShellCommands()
    {
        var windows = CreateSession("Windows");

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            windows,
            AgentReadOnlyToolCatalog.StatRemotePathToolName,
            Json(new { path = "C:\\ProgramData\\app\\config.json" }),
            out var stat,
            out var error), error);
        var statScript = DecodePowerShell(stat.Command);
        Assert.Contains("Test-Path -LiteralPath", statScript, StringComparison.Ordinal);
        Assert.Contains("Get-Item -LiteralPath", statScript, StringComparison.Ordinal);

        Assert.True(AgentReadOnlyToolCatalog.TryCreatePlan(
            windows,
            AgentReadOnlyToolCatalog.ReadRemoteFileToolName,
            Json(new { path = "C:\\ProgramData\\app\\config.json", lines = 30 }),
            out var read,
            out error), error);
        var readScript = DecodePowerShell(read.Command);
        Assert.Contains("Get-Content -LiteralPath", readScript, StringComparison.Ordinal);
        Assert.Contains("-TotalCount 30", readScript, StringComparison.Ordinal);
    }

    private static System.Text.Json.JsonElement Json(object value)
        => System.Text.Json.JsonSerializer.SerializeToElement(value);

    private static string DecodePowerShell(string command)
    {
        var encoded = command[(command.LastIndexOf(' ') + 1)..];
        return Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
    }

    private static AgentSessionSnapshot CreateSession(string platform)
        => new()
        {
            SessionId = Guid.NewGuid(),
            Protocol = SessionProtocol.SSH,
            IsConnected = true,
            Platform = platform
        };
}
