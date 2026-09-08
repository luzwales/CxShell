using System.Threading;
using FxShell.Models;

namespace FxShell.Services;

public interface IFileTransferService
{
    bool IsConnected { get; }

    event Action<string>? ErrorOccurred;

    Task ConnectAsync(SessionInfo session, string? password);

    void Disconnect();

    Task<string> GetHomeDirectoryAsync();

    Task<List<SftpFileItem>> ListDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        string localPath,
        string remotePath,
        Action<ulong>? progress = null,
        CancellationToken cancellationToken = default,
        bool resume = false);

    Task DownloadFileAsync(
        string remotePath,
        string localPath,
        Action<ulong>? progress = null,
        CancellationToken cancellationToken = default,
        bool resume = false);

    Task DeleteAsync(string remotePath, bool isDirectory);

    Task RenameAsync(string oldPath, string newPath);

    Task CreateDirectoryAsync(string path);
}
