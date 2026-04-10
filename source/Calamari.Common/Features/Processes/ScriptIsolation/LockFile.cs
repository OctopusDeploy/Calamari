using System;
using System.IO;
using System.Threading.Tasks;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockFile(
    LockDirectory Directory,
    FileInfo File
)
{
    public bool Exists => File.Exists;
    public bool IsFullySupported => Directory.LockSupport == LockCapability.Supported;
    public void Delete() => File.Delete();
    public bool Supports(LockType lockType) => Directory.Supports(lockType);

    public ILockHandle Open(LockType isolationLevel)
    {
        if (!Supports(isolationLevel))
        {
            throw new NotSupportedException($"Lock file {File.FullName} does not support {isolationLevel} locks");
        }

        var fileShareMode = GetFileShareMode(isolationLevel);
        var fileStream = File.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, fileShareMode);
        try
        {
            return new LockHandle(fileStream);
        }
        catch
        {
            // In the unlikely event of an exception
            fileStream.Dispose();
            throw;
        }
    }

    public static LockFile FromDirectory(LockDirectory directory, string name)
    {
        var lockFilePath = Path.Join(directory.DirectoryInfo.FullName, name);
        return new LockFile(directory, new FileInfo(lockFilePath));
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
