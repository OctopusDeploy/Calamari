using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public enum LockCapability
{
    Unknown,
    Unsupported,
    ExclusiveOnly,
    Supported
}
