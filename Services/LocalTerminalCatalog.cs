using CxShell.Models;

namespace CxShell.Services;

/// <summary>
/// Detects interactive shells that can be hosted by the local PTY backend.
/// Profiles are runtime-only; they are not part of the remote session store.
/// </summary>
public static class LocalTerminalCatalog
{
    public static IReadOnlyList<LocalTerminalProfile> Detect()
    {
        return OperatingSystem.IsWindows()
            ? DetectWindows()
            : OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
                ? DetectUnix()
                : [];
    }

    private static IReadOnlyList<LocalTerminalProfile> DetectWindows()
    {
        var profiles = new List<LocalTerminalProfile>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (FindOnPath("pwsh.exe") is { } pwsh)
        {
            profiles.Add(new(
                "pwsh",
                "PowerShell",
                pwsh,
                ["-NoLogo"],
                supportsPosixShellFeatures: false,
                workingDirectory: userProfile));
        }

        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var windowsPowerShell = Path.Combine(system32, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(windowsPowerShell))
        {
            profiles.Add(new(
                "powershell",
                "Windows PowerShell",
                windowsPowerShell,
                ["-NoLogo"],
                supportsPosixShellFeatures: false,
                workingDirectory: userProfile));
        }

        var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(system32, "cmd.exe");
        if (File.Exists(commandProcessor))
        {
            profiles.Add(new(
                "cmd",
                "Command Prompt",
                commandProcessor,
                [],
                supportsPosixShellFeatures: false,
                workingDirectory: userProfile));
        }

        var wsl = Path.Combine(system32, "wsl.exe");
        if (File.Exists(wsl))
        {
            profiles.Add(new("wsl", "WSL", wsl, [], true, userProfile));
        }

        if (FindGitBash() is { } gitBash)
        {
            profiles.Add(new(
                "gitbash",
                "Git Bash",
                gitBash,
                ["--login", "-i"],
                true,
                userProfile));
        }

        return profiles;
    }

    private static IReadOnlyList<LocalTerminalProfile> DetectUnix()
    {
        var profiles = new List<LocalTerminalProfile>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var addedPaths = new HashSet<string>(StringComparer.Ordinal);

        var defaultShell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrWhiteSpace(defaultShell) && File.Exists(defaultShell) && addedPaths.Add(defaultShell))
        {
            profiles.Add(CreateUnixProfile("default", GetUnixShellName(defaultShell), defaultShell, userProfile));
        }

        foreach (var candidate in new[] { "zsh", "bash", "fish", "pwsh" })
        {
            var path = FindOnPath(candidate);
            if (path == null || !addedPaths.Add(path))
                continue;

            var isPosix = !string.Equals(candidate, "pwsh", StringComparison.OrdinalIgnoreCase);
            profiles.Add(CreateUnixProfile(candidate, GetUnixShellName(path), path, userProfile, isPosix));
        }

        if (profiles.Count == 0 && File.Exists("/bin/sh"))
            profiles.Add(CreateUnixProfile("sh", "sh", "/bin/sh", userProfile));

        return profiles;
    }

    private static LocalTerminalProfile CreateUnixProfile(
        string id,
        string name,
        string path,
        string userProfile,
        bool supportsPosixShellFeatures = true)
    {
        var fileName = Path.GetFileName(path);
        var arguments = string.Equals(fileName, "pwsh", StringComparison.OrdinalIgnoreCase)
            ? (IReadOnlyList<string>)["-NoLogo"]
            : ["-i"];
        return new LocalTerminalProfile(id, name, path, arguments, supportsPosixShellFeatures, userProfile);
    }

    private static string GetUnixShellName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch (ArgumentException)
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    private static string? FindGitBash()
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
        };

        foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            var candidate = Path.Combine(root!, "Git", "bin", "bash.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        if (FindOnPath("git.exe") is { } git)
        {
            var gitRoot = Path.GetDirectoryName(Path.GetDirectoryName(git));
            if (!string.IsNullOrWhiteSpace(gitRoot))
            {
                var candidate = Path.Combine(gitRoot, "bin", "bash.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
