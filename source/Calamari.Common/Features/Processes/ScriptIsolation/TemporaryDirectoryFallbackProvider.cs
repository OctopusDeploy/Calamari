using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// Provides the real implementation of temporary directory candidate enumeration that
/// queries environment variables and the actual filesystem to build the list of
/// candidate temporary directories.
/// </summary>
class TemporaryDirectoryFallbackProvider
    : ITemporaryDirectoryFallbackProvider
{
    public IEnumerable<DirectoryInfo> GetFallbackCandidates(DirectoryInfo preferredDirectory)
    {
        var pathNamespace = $"octopus.{preferredDirectory.Name}";

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Path.Combine(
                                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                            "Calamari"
                                           );
            yield return ApplyNamespace(localAppData);
            yield return ApplyNamespace(Path.GetTempPath());
            yield break;
        }

        var tmpDir = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tmpDir))
        {
            yield return ApplyNamespace(tmpDir);
        }

        const string tmp = "/tmp";
        if (Directory.Exists(tmp))
        {
            yield return ApplyNamespace(tmp);
        }

        const string devShm = "/dev/shm";
        if (Directory.Exists(devShm))
        {
            yield return ApplyNamespace(devShm);
        }

        yield break;

        DirectoryInfo ApplyNamespace(string rawPath) => new(Path.Combine(rawPath, pathNamespace));
    }
}
