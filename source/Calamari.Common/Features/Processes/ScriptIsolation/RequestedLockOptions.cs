using System;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record RequestedLockOptions(
    LockType Type,
    string MutexName,
    TimeSpan Timeout,
    DirectoryInfo PreferredLockDirectory
);
