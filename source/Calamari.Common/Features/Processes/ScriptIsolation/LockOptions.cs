using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockOptions(
    LockType Type,
    string Name,
    LockFile LockFile,
    TimeSpan Timeout
);
