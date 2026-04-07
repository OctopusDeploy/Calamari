using System;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockDirectoryFallback(
    LockDirectoryFallbackType Type,
    DirectoryInfo Directory
);
