using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockDirectory(
    DirectoryInfo DirectoryInfo,
    LockCapability LockSupport,
    bool IsFallback,
    IReadOnlyList<LockSupportDetectionResult> DetectionResults
)
{
    public LockDirectory(DirectoryInfo directoryInfo, LockCapability lockSupport)
        : this(DirectoryInfo: directoryInfo, LockSupport: lockSupport, IsFallback: false, DetectionResults: [])
    {
    }

    public LockFile GetLockFile(string name)
    {
        return LockFile.FromDirectory(this, name);
    }

    public bool Supports(LockType lockType)
    {
        return LockSupport switch
               {
                   LockCapability.Supported => true,
                   LockCapability.ExclusiveOnly => lockType == LockType.Exclusive,
                   _ => false
               };
    }
}
