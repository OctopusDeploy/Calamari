using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// The real filesystem implementation of <see cref="IFileLockService"/>.
/// Uses <see cref="FileLock.Acquire"/> for lock acquisition and
/// <see cref="Directory.CreateDirectory(string)"/> for directory creation.
/// </summary>
internal sealed class FileLockService : IFileLockService
{
    FileLockService() { }

    public static readonly IFileLockService Instance = new FileLockService();

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public ILockHandle AcquireLock(LockOptions options) => FileLock.Acquire(options);
}
