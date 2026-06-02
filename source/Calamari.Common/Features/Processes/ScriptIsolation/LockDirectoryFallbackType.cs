using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public enum LockDirectoryFallbackType
{
    WindowsLocalAppData,
    WindowsTempPath,
    TempEnvironmentVariable,
    TempFixed,
    DevShm
}
