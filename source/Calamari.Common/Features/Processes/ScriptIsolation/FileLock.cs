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
        catch (IOException e)
        {
            throw new LockRejectedException(e);
        }
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
