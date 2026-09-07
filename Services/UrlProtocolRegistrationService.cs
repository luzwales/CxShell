using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CxShell.Services;

/// <summary>Registers only user-level ssh:// and sftp:// handlers.</summary>
public static class UrlProtocolRegistrationService
{
    private static readonly string[] Schemes = ["ssh", "sftp"];
    private const string ManagedValue = "CxShellManaged";

    public static void Apply(bool enabled)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                ApplyWindows(enabled);
            else if (OperatingSystem.IsLinux())
                ApplyLinux(enabled);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"CxShell URL protocol registration failed: {ex.Message}");
        }
    }

    private static string GetExecutablePath()
        => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    [SupportedOSPlatform("windows")]
    private static void ApplyWindows(bool enabled)
    {
        var executablePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        foreach (var scheme in Schemes)
        {
            var keyPath = $@"Software\Classes\{scheme}";
            if (!enabled)
            {
                if (IsManagedWindowsKey(keyPath))
                    Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                continue;
            }

            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            if (key is null)
                continue;
            key.SetValue(null, $"URL:{scheme.ToUpperInvariant()} Protocol");
            key.SetValue("URL Protocol", string.Empty);
            key.SetValue(ManagedValue, 1, RegistryValueKind.DWord);
            using var command = key.CreateSubKey(@"shell\open\command");
            command?.SetValue(null, $"\"{executablePath}\" -url \"%1\"");
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsManagedWindowsKey(string keyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(ManagedValue) is int marker && marker == 1;
    }

    [SupportedOSPlatform("linux")]
    private static void ApplyLinux(bool enabled)
    {
        var applicationsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications");
        var desktopPath = Path.Combine(applicationsPath, "cxshell-url-handler.desktop");

        if (!enabled)
        {
            if (File.Exists(desktopPath))
                File.Delete(desktopPath);
            return;
        }

        var executablePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        Directory.CreateDirectory(applicationsPath);
        var mimeTypes = string.Join(string.Empty, Schemes.Select(s => $"x-scheme-handler/{s};"));
        File.WriteAllText(desktopPath, $"""
[Desktop Entry]
Name=CxShell URL handler
Comment=Open ssh:// and sftp:// links in CxShell
Exec="{executablePath}" -url %u
Type=Application
Terminal=false
NoDisplay=true
MimeType={mimeTypes}
""");

        RunQuietly("update-desktop-database", applicationsPath);
        foreach (var scheme in Schemes)
            RunQuietly("xdg-mime", $"default cxshell-url-handler.desktop x-scheme-handler/{scheme}");
    }

    private static void RunQuietly(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            process?.WaitForExit(3000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Trace.WriteLine($"CxShell URL helper unavailable: {ex.Message}");
        }
    }
}
