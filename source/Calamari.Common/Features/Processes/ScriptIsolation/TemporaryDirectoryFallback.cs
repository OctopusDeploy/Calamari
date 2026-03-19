using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// Provides the real implementation of temporary directory candidate enumeration that
/// queries environment variables and the actual filesystem to build the list of
/// candidate temporary directories.
/// </summary>
static class TemporaryDirectoryFallback
{
    public static IEnumerable<string> GetCandidates(string candidatePath)
    {
        var pathNamespace = Path.GetFileName(candidatePath);

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Path.Combine(
                                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                            "Calamari",
                                            pathNamespace
                                           );
            var windowsTempPath = Path.GetTempPath();
            yield return localAppData;
            yield return windowsTempPath;
            yield break;
        }

        var tmpDir = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tmpDir))
        {
            yield return Path.Combine(tmpDir, pathNamespace);
        }

        const string tmp = "/tmp";
        if (Directory.Exists(tmp))
        {
            yield return Path.Combine(tmp, pathNamespace);
        }

        const string devShm = "/dev/shm";
        if (Directory.Exists(devShm))
        {
            yield return Path.Combine(devShm, pathNamespace);
        }
    }
}
