using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockDirectory(
    DirectoryInfo DirectoryInfo,
    LockCapability LockSupport
)
{
    public LockFile GetLockFile(string name)
    {
        return LockFile.FromDirectory(this, name);
    }

    public bool Supports(LockType lockType)
    {
        return LockSupport switch
               {
                   LockCapability.Supported => true,
                   LockCapability.ExclusiveOnly => lockType == LockType.Exclusive,
                   _ => false
               };
    }

    public static LockDirectory GetLockDirectory(string candidatePath)
    {
        var mountedDrives = MountedDrives.Get();

        var candidateDrive = mountedDrives.GetAssociatedDrive(candidatePath);

        if (candidateDrive.LockSupport == LockCapability.Supported)
        {
            return Supported(candidatePath);
        }

        string? tempPathExclusiveOnly = null;

        // Fall back immediately to somewhere under the temp directory
        var tempCandidates = GetTemporaryCandidates(candidatePath);
        foreach (var tempPath in tempCandidates)
        {
            var tempDrive = mountedDrives.GetAssociatedDrive(tempPath)
                                          .DetectLockSupport(tempPath);
            if (tempDrive.LockSupport == LockCapability.Supported)
            {
                return Supported(tempPath);
            }

            if (tempDrive.LockSupport == LockCapability.ExclusiveOnly)
            {
                // Catch the first temp path that supports exclusive locking
                tempPathExclusiveOnly ??= tempPath;
            }
        }

        // Go back to the original drive and check its support
        candidateDrive = candidateDrive.DetectLockSupport(candidatePath);
        if (candidateDrive.LockSupport == LockCapability.Supported)
        {
            return Supported(candidatePath);
        }

        if (tempPathExclusiveOnly is not null)
        {
            return new(
                       DirectoryInfo: new DirectoryInfo(tempPathExclusiveOnly),
                       LockSupport: LockCapability.ExclusiveOnly
                      );
        }

        return new(
                   DirectoryInfo: new DirectoryInfo(candidatePath),
                   LockSupport: LockCapability.Unsupported
                  );
    }

    static LockDirectory Supported(string path)
    {
        return new(
                   DirectoryInfo: new DirectoryInfo(path),
                   LockSupport: LockCapability.Supported
                  );
    }

    static string[] GetTemporaryCandidates(string candidatePath)
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
            return [localAppData, windowsTempPath];
        }

        var tempDirs = new List<string>();
        var tmpDir = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tmpDir))
        {
            tempDirs.Add(Path.Combine(tmpDir,  pathNamespace));
        }

        const string tmp = "/tmp";

        if (Directory.Exists(tmp))
        {
            tempDirs.Add(Path.Combine(tmp, pathNamespace));
        }

        return tempDirs.ToArray();
    }

    sealed record MountedDrives(CachedDriveInfo[] Drives)
    {
        public static MountedDrives Get()
        {
            try
            {
                var drives = DriveInfo.GetDrives()
                                      .Select(CachedDriveInfo.From)
                                      .OrderBy(d => d.RootDirectory.FullName)
                                      .ToArray();
                return new MountedDrives(drives);
            }
            catch
            {
                // Let's think about what we really want to do if this happens
                return new MountedDrives([]);
            }
        }

        public CachedDriveInfo GetAssociatedDrive(string path)
        {
            var result = Drives
                             .Where(d => IsAncestor(d.RootDirectory, path))
                             .OrderByDescending(d => d.RootDirectory.FullName.Length)
                             .FirstOrDefault();
            if (result is not null)
            {
                return result;
            }

            throw new DirectoryNotFoundException($"Unable to find the drive for '{path}'.");
        }

        static bool IsAncestor(DirectoryInfo ancestor, string path)
        {
            var ancestorPath = ancestor.FullName;
            if (!ancestorPath.EndsWith(Path.DirectorySeparatorChar))
            {
                ancestorPath += Path.DirectorySeparatorChar;
            }
            return path.StartsWith(ancestorPath, StringComparison.OrdinalIgnoreCase);
        }
    }

}
