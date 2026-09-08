using FxShell.Services;

namespace FxShell.Tests;

public sealed class SftpTransferPartPathsTests
{
    [Fact]
    public void LocalPathAppendsPartialSuffix()
    {
        Assert.Equal(
            @"C:\Downloads\archive.zip.fxshell.part",
            SftpTransferPartPaths.GetLocalPath(@"C:\Downloads\archive.zip"));
    }

    [Fact]
    public void RemotePathHidesPartialFileAlongsideNamedFile()
    {
        Assert.Equal(
            "/var/log/.archive.log.fxshell.part",
            SftpTransferPartPaths.GetRemotePath("/var/log/archive.log"));
    }

    [Fact]
    public void RemotePathWithoutDirectoryUsesSuffixOnly()
    {
        Assert.Equal(
            "archive.log.fxshell.part",
            SftpTransferPartPaths.GetRemotePath("archive.log"));
    }
}
