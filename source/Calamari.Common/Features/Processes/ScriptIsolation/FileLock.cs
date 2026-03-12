using System;
using System.IO;
using System.Threading.Tasks;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public static class FileLock
{
    public static ILockHandle Acquire(LockOptions lockOptions)
    {
        var fileShareMode = GetFileShareMode(lockOptions.Type);
        try
        {
            var fileStream = lockOptions.LockFile.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, fileShareMode);
            return new LockHandle(fileStream);
        }
        catch (IOException e) when (IsFileLocked(e))
        {
            throw new LockRejectedException(e);
        }
    }

    const int WindowsErrorSharingViolation = unchecked((int)0x80070020); // ERROR_SHARING_VIOLATION
    const int LinuxErrorAgainWouldBlock = 11; // EAGAIN / EWOULDBLOCK
    const int MacOsErrorAgainWouldBlock = 35; // EAGAIN / EWOULDBLOCK

    static bool IsFileLocked(IOException ioException)
    {
        if (OperatingSystem.IsWindows())
        {
            return ioException.HResult == WindowsErrorSharingViolation;
        }

        if (OperatingSystem.IsLinux())
        {
            return ioException.HResult == LinuxErrorAgainWouldBlock;
        }

        if (OperatingSystem.IsMacOS())
        {
            return ioException.HResult == MacOsErrorAgainWouldBlock;
        }

        return false;
    }

    static FileShare GetFileShareMode(LockType isolationLevel)
    {
        return isolationLevel switch
        {
            LockType.Exclusive => FileShare.None,
            LockType.Shared => FileShare.ReadWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(isolationLevel), isolationLevel, null)
        };
    }

    sealed class LockHandle(FileStream fileStream) : ILockHandle
    {
        public void Dispose()
        {
            fileStream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await fileStream.DisposeAsync();
        }
    }
}
