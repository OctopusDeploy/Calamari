using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockSupportDetectionResult(
    LockDirectoryFallbackType? FallbackType,
    string FileSystem,
    LockCapability LockCapability
);
