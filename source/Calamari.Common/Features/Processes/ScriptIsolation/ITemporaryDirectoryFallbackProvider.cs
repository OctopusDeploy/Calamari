using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

interface ITemporaryDirectoryFallbackProvider
{
    IEnumerable<DirectoryInfo> GetFallbackCandidates(DirectoryInfo preferredDirectory);
}
