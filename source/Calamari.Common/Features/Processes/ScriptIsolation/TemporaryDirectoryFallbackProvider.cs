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
    public IEnumerable<LockDirectoryFallback> GetFallbackCandidates(DirectoryInfo preferredDirectory)
    {
        var pathNamespace = $"octopus.{preferredDirectory.Name}";

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Path.Combine(
                                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                            "Calamari"
                                           );
            yield return new(
                             Type: LockDirectoryFallbackType.WindowsLocalAppData,
                             Directory: ApplyNamespace(localAppData)
                            );
            yield return new(
                             Type: LockDirectoryFallbackType.WindowsTempPath,
                             Directory: ApplyNamespace(Path.GetTempPath())
                            );
            yield break;
        }

        var tmpDir = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tmpDir))
        {
            yield return new(
                             Type: LockDirectoryFallbackType.TempEnvironmentVariable,
                             Directory: ApplyNamespace(tmpDir)
                            );
        }

        const string tmp = "/tmp";
        if (tmp != tmpDir && Directory.Exists(tmp))
        {
            yield return new(
                             Type: LockDirectoryFallbackType.TempFixed,
                             Directory: ApplyNamespace(tmp)
                            );
        }

        const string devShm = "/dev/shm";
        if (Directory.Exists(devShm))
        {
            yield return new(
                             Type: LockDirectoryFallbackType.DevShm,
                             Directory: ApplyNamespace(devShm)
                            );
        }

        yield break;

        DirectoryInfo ApplyNamespace(string rawPath) => new(Path.Combine(rawPath, pathNamespace));
    }
}
