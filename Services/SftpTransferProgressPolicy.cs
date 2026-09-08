using System;

namespace FxShell.Services;

public static class SftpTransferProgressPolicy
{
    public static ulong BeforeFinalize(ulong transferredBytes, long totalBytes)
    {
        if (totalBytes <= 0)
            return transferredBytes;

        var unfinishedLimit = (ulong)Math.Max(0, totalBytes - 1);
        return Math.Min(transferredBytes, unfinishedLimit);
    }
}
