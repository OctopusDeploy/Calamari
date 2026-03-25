using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockOptions(
    LockType Type,
    string Name,
    LockFile LockFile,
    TimeSpan Timeout
)
{
    /// <summary>
    /// Indicates whether file locking is supported for the configured location. This requires
    /// that both Exclusive and Shared locks are supported on the underlying filesystem.
    /// </summary>
    public bool IsFullySupported => LockFile.IsFullySupported;

    /// <summary>
    /// Indicates whether the specific type of lock is supported on the underlying file system.
    /// </summary>
    public bool IsSupported => LockFile.Supports(Type);
}
