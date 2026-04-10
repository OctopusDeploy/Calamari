using System;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public static class FileLock
{
    public static ILockHandle Acquire(LockOptions lockOptions)
    {
        try
        {
            return lockOptions.LockFile.Open(lockOptions.Type);
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
}
