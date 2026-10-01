using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public enum LockCapability
{
    /// <summary>
    /// Indicates that the location should not be relied on for file locking
    /// </summary>
    Unsupported = 0,
    /// <summary>
    /// Indicates that the location is only capable of exclusive file locks
    /// </summary>
    ExclusiveOnly,
    /// <summary>
    /// Indicates that the location supports both shared and exclusive file locks
    /// </summary>
    Supported
}
