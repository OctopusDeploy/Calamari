#nullable enable
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// Abstracts the filesystem operations required to probe lock support on a drive.
/// Separating these from the static implementations allows hermetic unit testing
/// without touching the real filesystem.
/// </summary>
internal interface IFileLockService
{
    /// <summary>Ensures the given directory path exists.</summary>
    void CreateDirectory(string path);

    /// <summary>Attempts to acquire a lock described by <paramref name="options"/>.</summary>
    /// <exception cref="LockRejectedException">
    ///   Thrown when the lock cannot be acquired due to a conflicting hold.
    /// </exception>
    /// <exception cref="System.IO.IOException">
    ///   Thrown when the filesystem does not support the requested lock type.
    /// </exception>
    ILockHandle AcquireLock(LockOptions options);
}
